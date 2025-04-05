using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.StationFramework;
using MelonLoader;
using UnityEngine;
using MelonLoader.Utils;
using Il2CppNewtonsoft.Json;
using System.Text.Json;

[assembly: MelonInfo(typeof(RecipeListGui.RecipeListGuiClass), "Recipe List", "1.0.5", " Rezx - Fork by ispa")]

namespace RecipeListGui
{
    public class RecipeListGuiClass : MelonMod
    {

        private class DataForFullIngredentsList
        {
            public string Name;
            public int Qnt;
        }

         
        private static Dictionary<string, string> _translationDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

         
        private static string Translate(string englishText)
        {
            if (string.IsNullOrEmpty(englishText))
                return englishText;

            if (_translationDictionary.TryGetValue(englishText, out string translation))
                return translation;

            return englishText;
        }

         
        private static void LoadTranslations()
        {
            string translationFilePath = Path.Combine(MelonEnvironment.GameRootDirectory, "Mods", "Translations", "recipe_translations.txt");

            Melon<RecipeListGuiClass>.Logger.Msg($"Попытка загрузить переводы из: {translationFilePath}");

            if (File.Exists(translationFilePath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(translationFilePath);
                    Melon<RecipeListGuiClass>.Logger.Msg($"Прочитано {lines.Length} строк из файла переводов");

                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                            continue;

                        string[] parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim();
                            string value = parts[1].Trim();
                            _translationDictionary[key] = value;
                        }
                    }
                    Melon<RecipeListGuiClass>.Logger.Msg($"Загружено {_translationDictionary.Count} переводов");
                }
                catch (Exception ex)
                {
                    Melon<RecipeListGuiClass>.Logger.Error($"Ошибка при загрузке переводов: {ex.Message}");
                }
            }
            else
            {
                 
                string translationDir = Path.Combine(MelonEnvironment.GameRootDirectory, "Mods", "Translations");
                if (!Directory.Exists(translationDir))
                {
                    try
                    {
                        Directory.CreateDirectory(translationDir);
                        Melon<RecipeListGuiClass>.Logger.Msg("Создана директория для переводов. Пожалуйста, добавьте файл recipe_translations.txt в папку Mods/Translations/");
                    }
                    catch (Exception ex)
                    {
                        Melon<RecipeListGuiClass>.Logger.Error($"Не удалось создать директорию для переводов: {ex.Message}");
                    }
                }
                else
                {
                    Melon<RecipeListGuiClass>.Logger.Msg("Файл переводов не найден. Пожалуйста, добавьте файл recipe_translations.txt в папку Mods/Translations/");
                }
            }
        }

         
        private static bool _showPriceFilterWindow = false;
        private static Rect _priceFilterWindowRect = new Rect(400, 20, 350, 180);
        private static float _minPrice = 0f;
        private static float _maxPrice = 1000f;
        private static bool _isPriceFilterActive = false;
        private static bool _sortByPriceAscending = true;
        private static bool _isSteamOverlayOpen = false;
         
        private static bool _showRecommendedWindow = false;
        private static Rect _recommendedWindowRect = new Rect(400, 220, 400, 400);
        private static Vector2 _recommendedScrollViewVector = Vector2.zero;
        private static List<RecommendedProduct> _recommendedProducts = new List<RecommendedProduct>();
        private static bool _isRecommendedListInitialized = false;
        private static int _recommendedSortMode = 0;  

         
        private class RecommendedProduct
        {
            public ProductDefinition Product;
            public float MarketValue;
            public float CostToMake;
            public float Profit;
            public float ProfitPercentage;
        }


        public override void OnInitializeMelon()
        {
            LoadTranslations();
            LoadWindowPositions();
            Melon<RecipeListGuiClass>.Logger.Msg("F5 to open");
            Melon<RecipeListGuiClass>.Logger.Msg("press F6 while gui is open to reset gui location");
        }

        public override void OnLateUpdate()
        {
             
             
            if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.Tab))
            {
                if (_guiShowen)
                {
                     
                    _isSteamOverlayOpen = true;
                    ToggleMenu();
                }
            }

             
            if (_isSteamOverlayOpen && !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            {
                _isSteamOverlayOpen = false;
                if (!_guiShowen)
                {
                    ToggleMenu();
                }
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                ToggleMenu();
            }
            if (Input.GetKeyDown(KeyCode.F6) && _guiShowen)
            {
                MelonLogger.Msg("F6 pressed reset gui location");
                ResetWindowPositions();
            }
        }


        private static bool _guiShowen;
        private static void ToggleMenu()
        {
            _guiShowen = !_guiShowen;
            if (_guiShowen)
            {
                MelonEvents.OnGUI.Subscribe(DrawPages, 50);
            }
            else
            {
                SaveWindowPositions();  
                _listOfCreatedProducts = null;
                MelonEvents.OnGUI.Unsubscribe(DrawPages);
            }
        }

        private static void UpdateRecommendedProducts()
        {
            _recommendedProducts.Clear();
            _listOfCreatedProducts ??= GetlistOfCreatedProducts();

            if (_listOfCreatedProducts == null)
                return;

            foreach (var product in _listOfCreatedProducts)
            {
                if (product.Recipes.Count >= 1)
                {
                    float costToMake = CalculateCostToMake(product, 0);
                    float profit = product.MarketValue - costToMake;
                    float profitPercentage = costToMake > 0 ? (profit / costToMake) * 100 : 0;

                    _recommendedProducts.Add(new RecommendedProduct
                    {
                        Product = product,
                        MarketValue = product.MarketValue,
                        CostToMake = costToMake,
                        Profit = profit,
                        ProfitPercentage = profitPercentage
                    });
                }
            }

             
            SortRecommendedProducts();
        }

        private static void SortRecommendedProducts()
        {
            switch (_recommendedSortMode)
            {
                case 0:  
                    _recommendedProducts = _recommendedProducts.OrderByDescending(p => p.Profit).ToList();
                    break;
                case 1:  
                    _recommendedProducts = _recommendedProducts.OrderByDescending(p => p.ProfitPercentage).ToList();
                    break;
                case 2:  
                    _recommendedProducts = _recommendedProducts.OrderByDescending(p => p.MarketValue).ToList();
                    break;
            }
        }

        private static float CalculateCostToMake(ProductDefinition product, int recipeIndex, HashSet<ProductDefinition> processedProducts = null)
        {
             
            if (processedProducts == null)
                processedProducts = new HashSet<ProductDefinition>();

             
            if (processedProducts.Contains(product))
                return 0;

             
            processedProducts.Add(product);

            float cost = 0;

            if (product.Recipes.Count < 1)
                return cost;

            StationRecipe recipe = product.Recipes.ToArray().ToList()[recipeIndex];

            GameObject productObject = GameObject.Find("@Product");
            if (productObject == null)
                return cost;

            ProductManager productManagerComp = productObject.GetComponent<ProductManager>();
            if (productManagerComp == null)
                return cost;

            foreach (var ingredient in recipe.Ingredients)
            {
                string cat = ingredient.Item.Category.ToString();

                if (cat == "Consumable" || cat == "Ingredient")
                {
                    StorableItemDefinition prop = ingredient.Item.TryCast<StorableItemDefinition>();
                    if (prop != null)
                    {
                        cost += prop.BasePurchasePrice * ingredient.Quantity;
                    }
                }
                else if (cat == "Product")
                {
                    ProductDefinition drug = ingredient.Item.TryCast<ProductDefinition>();
                    if (drug != null)
                    {
                        if (productManagerComp.DefaultKnownProducts.Contains(drug))
                        {
                            cost += drug.BasePrice * ingredient.Quantity;
                        }
                        else if (drug.Recipes.Count >= 1 && drug != product)  
                        {
                            cost += CalculateCostToMake(drug, 0, processedProducts) * ingredient.Quantity;
                        }
                    }
                }
            }

            return cost;
        }

        private static void RecommendedProductsWindow(int windowId)
        {
             
            if (GUI.Button(new Rect(_recommendedWindowRect.width - 30, 5, 25, 20), "X"))
            {
                _showRecommendedWindow = false;
            }

             
            if (GUI.Button(new Rect(_recommendedWindowRect.width - 60, 5, 25, 20), "↻"))
            {
                UpdateRecommendedProducts();
            }

             
            GUI.Label(new Rect(10, 30, 100, 20), Translate("Sort by:"));

            if (GUI.Button(new Rect(110, 30, 70, 20), Translate("Profit")))
            {
                _recommendedSortMode = 0;
                SortRecommendedProducts();
            }

            if (GUI.Button(new Rect(185, 30, 70, 20), Translate("Profit %")))
            {
                _recommendedSortMode = 1;
                SortRecommendedProducts();
            }

            if (GUI.Button(new Rect(260, 30, 70, 20), Translate("Price")))
            {
                _recommendedSortMode = 2;
                SortRecommendedProducts();
            }

             
            GUI.Label(new Rect(10, 55, 140, 20), Translate("Product"));
            GUI.Label(new Rect(150, 55, 60, 20), Translate("Price"));
            GUI.Label(new Rect(210, 55, 60, 20), Translate("Cost"));
            GUI.Label(new Rect(270, 55, 60, 20), Translate("Profit"));

             
            GUI.Box(new Rect(10, 75, _recommendedWindowRect.width - 20, 2), "");

             
            _recommendedScrollViewVector = GUI.BeginScrollView(
                new Rect(10, 80, _recommendedWindowRect.width - 20, _recommendedWindowRect.height - 90),
                _recommendedScrollViewVector,
                new Rect(0, 0, _recommendedWindowRect.width - 40, _recommendedProducts.Count * 25)
            );

            for (int i = 0; i < _recommendedProducts.Count; i++)
            {
                var product = _recommendedProducts[i];

                 
                if (i % 2 == 0)
                {
                    GUI.Box(new Rect(0, i * 25, _recommendedWindowRect.width - 40, 25), "");
                }

                 
                if (GUI.Button(new Rect(5, i * 25, 140, 20), Translate(product.Product.name)))
                {
                    _selectedBud = product.Product;
                    _hasSelectedBud = true;
                    _ingredientListRecipePage = null;
                    _hasSelectedProductRecipe = false;
                    _selectedProductRecipeIndex = 0;
                }

                 
                GUI.Label(new Rect(150, i * 25, 60, 20), $"${product.MarketValue:F1}");
                GUI.Label(new Rect(210, i * 25, 60, 20), $"${product.CostToMake:F1}");

                 
                string profitText = $"${product.Profit:F1}";
                if (_recommendedSortMode == 1)
                {
                    profitText = $"{product.ProfitPercentage:F1}%";
                }

                GUIStyle profitStyle = new GUIStyle(GUI.skin.label);
                profitStyle.normal.textColor = product.Profit > 0 ? Color.green : Color.red;
                GUI.Label(new Rect(270, i * 25, 60, 20), profitText, profitStyle);
            }

            GUI.EndScrollView();

             
            GUI.DragWindow(new Rect(0, 0, _recommendedWindowRect.width, 25));
        }
        private static void DrawPages()
        {
            if (_isSteamOverlayOpen)
                return;

            Texture2D transparentTex = new Texture2D(1, 1);
            transparentTex.SetPixel(0, 0, new Color(Color.gray.r, Color.gray.g, Color.gray.b, 0.56f));
            transparentTex.Apply();

            GUIStyle customWindowStyle = new GUIStyle(GUI.skin.window);
            customWindowStyle.normal.textColor = Color.white;
            customWindowStyle.normal.background = transparentTex;

            _productListPageRect = GUI.Window(651, _productListPageRect, (GUI.WindowFunction)ProductListPage, Translate("Product List"), customWindowStyle);
            _favsListPageRect = GUI.Window(652, _favsListPageRect, (GUI.WindowFunction)FavListPage, Translate("Favorite List"), customWindowStyle);
            if (_hasSelectedBud)
            {
                _recipeResultPageRect = GUI.Window(653, _recipeResultPageRect, (GUI.WindowFunction)RecipePage, Translate("Recipe"), customWindowStyle);
            }

             
            if (_showPriceFilterWindow)
            {
                _priceFilterWindowRect = GUI.Window(654, _priceFilterWindowRect, (GUI.WindowFunction)PriceFilterWindow, Translate("Price Filter"), customWindowStyle);
            }

            if (_showRecommendedWindow)
            {
                _recommendedWindowRect = GUI.Window(655, _recommendedWindowRect, (GUI.WindowFunction)RecommendedProductsWindow, Translate("Recommended Products"), customWindowStyle);
            }
        }

         
        private static void PriceFilterWindow(int windowId)
        {
             
            GUI.Label(new Rect(20, 30, 310, 20), Translate("Enter price range to filter products:"));

             
            GUI.Label(new Rect(20, 60, 80, 20), Translate("Min Price:"));
            GUI.Label(new Rect(100, 60, 80, 20), $"${_minPrice}");

            GUI.Label(new Rect(20, 100, 80, 20), Translate("Max Price:"));
            GUI.Label(new Rect(100, 100, 80, 20), $"${_maxPrice}");

             
            if (GUI.Button(new Rect(180, 60, 30, 20), "-"))
            {
                _minPrice = Math.Max(0, _minPrice - 10);
            }
            if (GUI.Button(new Rect(215, 60, 30, 20), "+"))
            {
                _minPrice = Math.Min(_maxPrice, _minPrice + 10);
            }

             
            if (GUI.Button(new Rect(250, 60, 40, 20), "-100"))
            {
                _minPrice = Math.Max(0, _minPrice - 100);
            }
            if (GUI.Button(new Rect(295, 60, 40, 20), "+100"))
            {
                _minPrice = Math.Min(_maxPrice, _minPrice + 100);
            }

             
            if (GUI.Button(new Rect(180, 100, 30, 20), "-"))
            {
                _maxPrice = Math.Max(_minPrice, _maxPrice - 10);
            }
            if (GUI.Button(new Rect(215, 100, 30, 20), "+"))
            {
                _maxPrice += 10;
            }

             
            if (GUI.Button(new Rect(250, 100, 40, 20), "-100"))
            {
                _maxPrice = Math.Max(_minPrice, _maxPrice - 100);
            }
            if (GUI.Button(new Rect(295, 100, 40, 20), "+100"))
            {
                _maxPrice += 100;
            }

             
            if (GUI.Button(new Rect(20, 140, 150, 25), Translate("Apply Filter")))
            {
                _isPriceFilterActive = true;
                _showPriceFilterWindow = false;

                 
                _productListPageScrollViewVector = Vector2.zero;
            }

             
            if (GUI.Button(new Rect(180, 140, 150, 25), Translate("Reset Filter")))
            {
                _isPriceFilterActive = false;
                _minPrice = 0f;
                _maxPrice = 1000f;
            }

             
            if (GUI.Button(new Rect(_priceFilterWindowRect.width - 30, 5, 25, 20), "X"))
            {
                _showPriceFilterWindow = false;
            }

             
            GUI.DragWindow(new Rect(0, 0, _priceFilterWindowRect.width, 25));
        }

        private static Il2CppSystem.Collections.Generic.List<ProductDefinition> GetlistOfCreatedProducts()
        {
            GameObject productObject = GameObject.Find("@Product");
            if (productObject == null)
            {
                 
                return null;
            }
            ProductManager productManagerComp = productObject.GetComponent<ProductManager>();
            if (productManagerComp == null)
            {
                 
                return null;
            }
            return productManagerComp.AllProducts;
        }


        private static Il2CppSystem.Collections.Generic.List<ProductDefinition> GetlistOf_FavProducts()
        {
            GameObject productObject = GameObject.Find("@Product");
            if (productObject == null)
            {
                 
                return null;
            }
            ProductManager productManagerComp = productObject.GetComponent<ProductManager>();
            if (productManagerComp == null)
            {
                 
                return null;
            }
            Il2CppSystem.Collections.Generic.List<ProductDefinition> favourites = ProductManager.FavouritedProducts;

            return favourites;
        }


        private static float _costToMake;
        static Il2CppSystem.Collections.Generic.List<string> GetIngredientList(ProductDefinition product, int selectedProductRecipe)
        {
            Il2CppSystem.Collections.Generic.List<string> outputLines = new();
            List<DataForFullIngredentsList> ingredientListTemp = new();

            _costToMake = 0;
             
            ProcessProduct(product, outputLines, selectedProductRecipe, true, ingredientListTemp);
             
            int totalIngredients = ingredientListTemp.Sum(i => i.Qnt);

            outputLines.Add("-----------------------------------------------------------------------------------");
            outputLines.Add($"{Translate("Market Price")}: ${product.MarketValue}, {Translate("Ingredients")}: {totalIngredients}, {Translate("Ingredients Cost")}: ${_costToMake}");
            outputLines.Add("-----------------------------------------------------------------------------------");

            foreach (var ingredient in ingredientListTemp)
            {
                outputLines.Add($"{ingredient.Qnt}x {Translate(ingredient.Name)} ");
            }
            return outputLines;
        }
        private static Dictionary<string, string> _effectColors = new Dictionary<string, string>
{
    { "Anti-Gravity", "#235BCD" },
    { "Athletic", "#75C8FD" },
    { "Balding", "#C79232" },
    { "Bright-Eyed", "#BEF7FD" },
    { "Calming", "#FED09B" },
    { "Calorie-Dense", "#FE84F4" },
    { "Cyclopean", "#FEC174" },
    { "Disorienting", "#D16546" },
    { "Electrifying", "#55C8FD" },
    { "Energizing", "#9AFE6D" },
    { "Euphoric", "#FEEA74" },
    { "Explosive", "#FE4B40" },
    { "Focused", "#75F1FD" },
    { "Foggy", "#B0B0AF" },
    { "Gingeritis", "#FE8829" },
    { "Glowing", "#85E459" },
    { "Jennerising", "#FE8DF8" },
    { "Laxative", "#763C25" },
    { "Lethal", "#AB2232" },
    { "Long faced", "#FED961" },
    { "Munchies", "#C96E57" },
    { "Paranoia", "#C46762" },
    { "Refreshing", "#B2FE98" },
    { "Schizophrenic", "#645AFD" },
    { "Sedating", "#6B5FD8" },
    { "Seizure-Inducing", "#FEE900" },
    { "Shrinking", "#B6FEDA" },
    { "Slippery", "#A2DFFD" },
    { "Smelly", "#7DBC31" },
    { "Sneaky", "#7B7B7B" },
    { "Spicy", "#FE6B4C" },
    { "Thought-Provoking", "#FEA0CB" },
    { "Toxic", "#5F9A31" },
    { "Tropic Thunder", "#FE9F47" },
    { "Zombifying", "#71AB5D" }
};
        static void ProcessProduct(ProductDefinition product, Il2CppSystem.Collections.Generic.List<string> outputLines, int recipeToUse, bool isSelectedProductRecipe, System.Collections.Generic.List<DataForFullIngredentsList> ingredientListTemp)
        {
             

            if (!isSelectedProductRecipe && product == _selectedBud)
            {
                return;
            }

            GameObject productObject = GameObject.Find("@Product");
            if (productObject == null)
            {
                 
                return;
            }
            ProductManager productManagerComp = productObject.GetComponent<ProductManager>();
            if (productManagerComp == null)
            {
                 
                return;
            }

            if (product.Recipes.Count >= 1 && !productManagerComp.DefaultKnownProducts.Contains(product))
            {
                var recipes = product.Recipes;

                StationRecipe recipe = recipes.ToArray().ToList()[recipeToUse];  

                if (isSelectedProductRecipe)  
                {
                    outputLines.Add($"{Translate("Recipe for")} ({Translate(recipe.RecipeTitle)}) ");
                    string effects = "";
                    foreach (var effect in product.Properties)
                    {
                        string effectName = Translate(effect.Name);
                        string coloredEffect = effectName;

                         
                        if (_effectColors.TryGetValue(effect.Name, out string colorCode))
                        {
                            coloredEffect = $"<color={colorCode}>{effectName}</color>";
                        }

                        if (effects == "")
                        {
                            effects = coloredEffect;
                        }
                        else
                        {
                            effects = $"{effects}, {coloredEffect}";
                        }
                    }
                    outputLines.Add(effects);
                    outputLines.Add($"-----------------------------------------------------------------------------------");

                    isSelectedProductRecipe = false;
                }
                else
                {
                     
                    outputLines.Add($"<color=green>{Translate("Recipe")}: {Translate(recipe.RecipeTitle)}</color>");
                }
                 
                Il2CppSystem.Collections.Generic.List<ProductDefinition> nestedProducts = new Il2CppSystem.Collections.Generic.List<ProductDefinition>();

                foreach (var ingredient in recipe.Ingredients)
                {
                    string cat = ingredient.Item.Category.ToString();

                    if (cat == "Consumable" || cat == "Ingredient")
                    {
                        StorableItemDefinition prop = ingredient.Item.TryCast<StorableItemDefinition>();
                         

                        if (prop != null)
                        {
                            outputLines.Add($"{ingredient.Quantity}x {Translate(prop.Name)} {prop.BasePurchasePrice}$");
                             
                            var existingIngredient = ingredientListTemp.ToArray().ToList().FirstOrDefault(i => i.Name == prop.Name);
                            if (existingIngredient != null)
                            {
                                existingIngredient.Qnt += ingredient.Quantity;
                            }
                            else
                            {
                                ingredientListTemp.Add(new DataForFullIngredentsList() { Name = prop.Name, Qnt = ingredient.Quantity });
                            }

                            _costToMake += prop.BasePurchasePrice * ingredient.Quantity;

                        }
                        else
                        {
                            outputLines.Add($"FAILEDx Ingredient: {ingredient.Item}");
                            Melon<RecipeListGuiClass>.Logger.Msg($"ERROR ProcessProduct: Drug null {cat}");
                        }
                    }
                    else if (cat == "Product")
                    {
                         
                        ProductDefinition drug = ingredient.Item.TryCast<ProductDefinition>();
                        if (drug != null)
                        {
                            if (productManagerComp.DefaultKnownProducts.Contains(drug))
                            {
                                _costToMake += drug.BasePrice;
                                outputLines.Add($"{ingredient.Quantity}x {Translate(drug.Name)}, {Translate("Seed")}: {drug.BasePrice}$");

                            }
                            else
                            {
                                outputLines.Add($"{ingredient.Quantity}x {Translate(drug.Name)}");
                            }
                             
                            if (drug.Recipes.Count >= 1)
                            {
                                nestedProducts.Add(drug);
                            }
                        }
                        else
                        {
                            outputLines.Add($"{ingredient.Quantity}xIngredient: {ingredient.Item}");
                            Melon<RecipeListGuiClass>.Logger.Msg($"ERROR ProcessProduct: Drug null {cat}");
                        }
                    }
                    else
                    {
                        outputLines.Add($"{ingredient.Quantity}xIngredient: {ingredient.Item}");
                        Melon<RecipeListGuiClass>.Logger.Msg($"ERROR ProcessProduct: Unknown Category {cat}");
                    }
                }

                 
                foreach (var nestedProduct in nestedProducts)
                {
                    ProcessProduct(nestedProduct, outputLines, 0, false, ingredientListTemp);  
                }
            }
        }


        private static Il2CppSystem.Collections.Generic.List<string> _ingredientListRecipePage;
        private static Vector2 _recipeResultPageScrollViewVector = Vector2.zero;
        private static Rect _recipeResultPageRect = new Rect(600, 20, 600, 600);
        private static bool _hasSelectedProductRecipe;
        private static int _selectedProductRecipeIndex;
        private static ProductDefinition _lastSelectedBud;
        static void RecipePage(int windowId)
        {
            Rect resizeHandleRect = new Rect(_recipeResultPageRect.width - 50, _recipeResultPageRect.height - 50, 25, 25);
            GUI.Box(resizeHandleRect, "");

             
            if (GUI.Button(new Rect(_recipeResultPageRect.width - 45, 10, 30, 30), "X"))
            {
                _selectedBud = null;
                _hasSelectedBud = false;
                return;
            }
             
             
            if (_selectedBud.Recipes.Count > 1)
            {
                if (_hasSelectedProductRecipe)
                {
                     
                    if (GUI.Button(new Rect(_recipeResultPageRect.width - 85, 20, 30, 30), Translate("B")))
                    {
                        _hasSelectedProductRecipe = false;
                        _ingredientListRecipePage = null;
                    }
                }


                if (!_hasSelectedProductRecipe)
                {
                    for (int i = 0; i < _selectedBud.Recipes.Count; i++)
                    {
                        if (GUI.Button(new Rect(_recipeResultPageRect.width / 3, 50 + 20 * i, 200, 20), $"{Translate("Recipe")} {i + 1}"))
                        {
                            _hasSelectedProductRecipe = true;

                            _selectedProductRecipeIndex = i;
                        }
                    }

                    ProcessResize(resizeHandleRect);

                    GUI.DragWindow(new Rect(0, 0, _recipeResultPageRect.width, _recipeResultPageRect.height - 30));

                    return;
                }

            }

            if (_selectedBud != null)
            {
                 
                if (_ingredientListRecipePage == null || _selectedBud != _lastSelectedBud)
                {
                    _ingredientListRecipePage = GetIngredientList(_selectedBud, _selectedProductRecipeIndex);

                    _lastSelectedBud = _selectedBud;
                }

                 
                GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
                headerStyle.alignment = TextAnchor.MiddleCenter;
                headerStyle.fontSize = 14;
                headerStyle.fontStyle = FontStyle.Bold;
                GUI.Label(new Rect(55, 20, _recipeResultPageRect.width - 105, 20), Translate("Recipe Details"), headerStyle);

                 
                GUI.Box(new Rect(55, 40, _recipeResultPageRect.width - 105, 2), "");

                _recipeResultPageScrollViewVector = GUI.BeginScrollView(
                    new Rect(55, 45, _recipeResultPageRect.width - 55, _recipeResultPageRect.height - 55),
                    _recipeResultPageScrollViewVector,
                    new Rect(0, 0, _recipeResultPageRect.width - 75, _ingredientListRecipePage.Count * 25)
                );

                 
                var ingredientListTemp = _ingredientListRecipePage.ToArray().ToList();
                for (int i = 0; i < _ingredientListRecipePage.Count; i++)
                {
                    string ingredientText = ingredientListTemp[i];

                     
                    GUIStyle textStyle = new GUIStyle(GUI.skin.label);
                    textStyle.normal.textColor = Color.white;
                    textStyle.fontSize = 12;

                     
                    if (ingredientText.StartsWith(Translate("Recipe") + ":"))
                    {
                         
                        textStyle.normal.textColor = Color.green;
                    }

                    GUI.Label(new Rect(10, i * 25 + 3, _recipeResultPageRect.width - 115, 20), ingredientText, textStyle);
                }

                GUI.EndScrollView();
            }


             
            ProcessResize(resizeHandleRect);
            GUI.DragWindow(new Rect(0, 0, _recipeResultPageRect.width, _recipeResultPageRect.height - 30));
        }


        private static Vector2 _productListPageScrollViewVector = Vector2.zero;
        private static Rect _productListPageRect = new Rect(100, 20, 350, 55);
        private static Il2CppSystem.Collections.Generic.List<ProductDefinition>? _listOfCreatedProducts;
        private static bool _hasSelectedProductType;
        private static string _typeOfDrugToFilter = "";
        private static bool _hasSelectedBud;
        private static ProductDefinition _selectedBud;
        private static bool _shouldMinimizeProductListPage = true;
        static void ProductListPage(int windowID)
        {
            if (_shouldMinimizeProductListPage)
            {
                if (GUI.Button(new Rect(_productListPageRect.width - 25, 2, 18, 17), "+"))
                {
                    _shouldMinimizeProductListPage = false;
                     
                    _productListPageRect = new Rect(_productListPageRect.x, _productListPageRect.y, 350, 400);
                }

                 
                GUIStyle versionStyle = new GUIStyle(GUI.skin.label);
                versionStyle.normal.textColor = Color.white;
                versionStyle.fontSize = 10;
                versionStyle.alignment = TextAnchor.MiddleLeft;

                GUI.Label(new Rect(10, 2, 100, 17), "v1.0.5", versionStyle);

                GUI.DragWindow(new Rect(20, 10, 500, 500));
                return;
            }
            else
            {
                if (GUI.Button(new Rect(_productListPageRect.width - 25, 2, 17, 17), "-"))
                {
                    _shouldMinimizeProductListPage = true;
                     
                    _productListPageRect = new Rect(_productListPageRect.x, _productListPageRect.y, 350, 55);
                    return;
                }

                 
                GUIStyle versionStyle = new GUIStyle(GUI.skin.label);
                versionStyle.normal.textColor = Color.white;
                versionStyle.fontSize = 10;
                versionStyle.alignment = TextAnchor.MiddleLeft;

                GUI.Label(new Rect(10, 2, 100, 17), "v1.0.4", versionStyle);
            }

             
            if (!_shouldMinimizeProductListPage)
            {
                GUIStyle creditStyle = new GUIStyle(GUI.skin.label);
                creditStyle.normal.textColor = Color.white;
                creditStyle.fontSize = 12;
                creditStyle.alignment = TextAnchor.MiddleCenter;

                 
                string creditText = "Created by ispa | Discord: discord.gg/NDDJzknp";

                 
                Vector2 textSize = creditStyle.CalcSize(new GUIContent(creditText));

                 
                GUI.Label(new Rect((_productListPageRect.width - textSize.x) / 2, _productListPageRect.height - 30, textSize.x, 20), creditText, creditStyle);

                 
                if (GUI.Button(new Rect((_productListPageRect.width - textSize.x) / 2 + creditText.IndexOf("discord.gg"), _productListPageRect.height - 30, 120, 20), "", new GUIStyle()))
                {
                     
                    Application.OpenURL("https://discord.gg/NDDJzknp");
                }
            }

            _listOfCreatedProducts ??= GetlistOfCreatedProducts();
            if (_listOfCreatedProducts == null)
            {
                GUI.DragWindow(new Rect(20, 10, 500, 500));
                return;
            }

            if (!_hasSelectedProductType)
            {
                 
                float buttonWidth = 200;
                float buttonX = (_productListPageRect.width - buttonWidth) / 2;
                float buttonSpacing = 10;  

                if (GUI.Button(new Rect(buttonX, 20, buttonWidth, 20), Translate("Marijuana")))
                {
                    _hasSelectedProductType = true;
                    _typeOfDrugToFilter = "Marijuana";
                }
                if (GUI.Button(new Rect(buttonX, 20 + 20 + buttonSpacing, buttonWidth, 20), Translate("Methamphetamine")))
                {
                    _hasSelectedProductType = true;
                    _typeOfDrugToFilter = "Methamphetamine";
                }
                if (GUI.Button(new Rect(buttonX, 20 + (20 + buttonSpacing) * 2, buttonWidth, 20), Translate("Cocaine")))
                {
                    _hasSelectedProductType = true;
                    _typeOfDrugToFilter = "Cocaine";
                }
                if (GUI.Button(new Rect(buttonX, 20 + (20 + buttonSpacing) * 3, buttonWidth, 20), Translate("All Products")))
                {
                    _hasSelectedProductType = true;
                }

                 
                if (GUI.Button(new Rect(buttonX, 20 + (20 + buttonSpacing) * 4, buttonWidth, 20), Translate("Filter by Price")))
                {
                    _showPriceFilterWindow = true;
                }

                 
                if (GUI.Button(new Rect(buttonX, 20 + (20 + buttonSpacing) * 5, buttonWidth, 20), Translate("Recommended Products")))
                {
                    _showRecommendedWindow = true;
                    if (!_isRecommendedListInitialized)
                    {
                        UpdateRecommendedProducts();
                        _isRecommendedListInitialized = true;
                    }
                }

                GUI.DragWindow(new Rect(20, 10, 500, 500));
                return;
            }
            else
            {
                 
                if (GUI.Button(new Rect(_productListPageRect.width - 37, 20, 29, 27), "B"))
                {
                    _hasSelectedProductType = false;
                    _typeOfDrugToFilter = "";
                    _productListPageScrollViewVector = Vector2.zero;
                }

                 
                if (GUI.Button(new Rect(_productListPageRect.width - 37, 50, 29, 27), "$"))
                {
                    _showPriceFilterWindow = true;
                }

                 
                if (GUI.Button(new Rect(_productListPageRect.width - 37, 80, 29, 27), _sortByPriceAscending ? "↑$" : "↓$"))
                {
                    _sortByPriceAscending = !_sortByPriceAscending;
                }
            }

            var sortedProducts = _listOfCreatedProducts.ToArray().ToList();
            if (_typeOfDrugToFilter != "")
            {
                sortedProducts = sortedProducts.Where(product => product.DrugType.ToString() == _typeOfDrugToFilter).ToList();
            }

             
            if (_isPriceFilterActive)
            {
                sortedProducts = sortedProducts.Where(product =>
                    product.MarketValue >= _minPrice && product.MarketValue <= _maxPrice).ToList();
            }

             
            if (_sortByPriceAscending)
            {
                sortedProducts = sortedProducts.OrderBy(product => product.MarketValue).ToList();
            }
            else
            {
                sortedProducts = sortedProducts.OrderByDescending(product => product.MarketValue).ToList();
            }

             
            GUI.Label(new Rect(10, 20, 140, 20), Translate("Product"));
            GUI.Label(new Rect(150, 20, 60, 20), Translate("Price"));

             
            GUI.Box(new Rect(10, 45, _productListPageRect.width - 60, 2), "");

             
            float scrollViewHeight = _productListPageRect.height - 90;  

             
            _productListPageScrollViewVector = GUI.BeginScrollView(
                new Rect(10, 50, _productListPageRect.width - 60, scrollViewHeight),
                _productListPageScrollViewVector,
                new Rect(0, 0, _productListPageRect.width - 80, sortedProducts.Count * 25)
            );

            for (int i = 0; i < sortedProducts.Count; i++)
            {
                var product = sortedProducts[i];

                 
                if (i % 2 == 0)
                {
                    GUI.Box(new Rect(0, i * 25, _productListPageRect.width - 40, 25), "");
                }

                 
                if (GUI.Button(new Rect(5, i * 25 + 2, 140, 20), Translate(product.name)))
                {
                    _selectedBud = product;
                    _hasSelectedBud = true;
                    _ingredientListRecipePage = null;
                    _hasSelectedProductRecipe = false;
                    _selectedProductRecipeIndex = 0;
                }

                 
                GUI.Label(new Rect(150, i * 25 + 2, 60, 20), $"${product.MarketValue}");
            }

            GUI.EndScrollView();

            GUI.DragWindow(new Rect(20, 10, 500, 500));
        }



        private static Vector2 _favsListPageScrollViewVector = Vector2.zero;
        private static Rect _favsListPageRect = new Rect(100, 325, 350, 55);
        private static Il2CppSystem.Collections.Generic.List<ProductDefinition>? _listOf_FavsProducts;
        private static bool _shouldMinimizeFavListPage = true;

        static void FavListPage(int windowID)
        {

            if (_shouldMinimizeFavListPage)
            {
                if (GUI.Button(new Rect(_favsListPageRect.width - 25, 2, 18, 17), "+"))
                {
                    _shouldMinimizeFavListPage = false;
                     
                    _favsListPageRect = new Rect(_favsListPageRect.x, _favsListPageRect.y, 350, 400);
                    return;
                }
                GUI.DragWindow(new Rect(20, 10, 500, 500));
                return;

            }
            else
            {
                if (GUI.Button(new Rect(_favsListPageRect.width - 25, 2, 17, 17), "-"))
                {
                    _shouldMinimizeFavListPage = true;
                     
                    _favsListPageRect = new Rect(_favsListPageRect.x, _favsListPageRect.y, 350, 55);
                    return;
                }
            }

            _listOf_FavsProducts ??= GetlistOf_FavProducts();
            if (_listOf_FavsProducts == null)
            {
                 
                return;
            }

             
            var filteredFavProducts = _listOf_FavsProducts.ToArray().ToList();
            if (_isPriceFilterActive)
            {
                filteredFavProducts = filteredFavProducts.Where(product =>
                    product.MarketValue >= _minPrice && product.MarketValue <= _maxPrice).ToList();
            }

             
            if (_sortByPriceAscending)
            {
                filteredFavProducts = filteredFavProducts.OrderBy(product => product.MarketValue).ToList();
            }
            else
            {
                filteredFavProducts = filteredFavProducts.OrderByDescending(product => product.MarketValue).ToList();
            }

             
            int buttonAreaHeight = 50;  

             
            GUI.Label(new Rect(10, 20, 140, 20), Translate("Product"));
            GUI.Label(new Rect(150, 20, 60, 20), Translate("Price"));

             
            GUI.Box(new Rect(10, 45, _favsListPageRect.width - 60, 2), "");

             
            _favsListPageScrollViewVector = GUI.BeginScrollView(
                new Rect(10, 50, _favsListPageRect.width - 60, _favsListPageRect.height - 60),
                _favsListPageScrollViewVector,
                new Rect(0, 0, _favsListPageRect.width - 80, filteredFavProducts.Count * 25)
            );

            for (int i = 0; i < filteredFavProducts.Count; i++)
            {
                var product = filteredFavProducts[i];

                 
                if (i % 2 == 0)
                {
                    GUI.Box(new Rect(0, i * 25, _favsListPageRect.width - 40, 25), "");
                }

                 
                if (GUI.Button(new Rect(5, i * 25 + 2, 140, 20), Translate(product.name)))
                {
                    _selectedBud = product;
                    _hasSelectedBud = true;
                    _ingredientListRecipePage = null;
                    _hasSelectedProductRecipe = false;
                    _selectedProductRecipeIndex = 0;
                }

                 
                GUI.Label(new Rect(150, i * 25 + 2, 60, 20), $"${product.MarketValue}");
            }

            GUI.EndScrollView();
            GUI.DragWindow(new Rect(20, 10, 500, 500));
        }


        private static bool _isResizing;
        private static Vector2 _initialMousePosition;
        private static Rect _initialWindowRect;
        private static Vector2 _minWindowSize = new Vector2(375, 400);
        static void ProcessResize(Rect handleRect)
        {
            Event currentEvent = Event.current;
            Vector2 mousePos = currentEvent.mousePosition;
            Vector2 screenMousePos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            switch (currentEvent.type)
            {
                case EventType.MouseDown:
                    if (handleRect.Contains(mousePos))
                    {
                        _isResizing = true;
                        _initialMousePosition = screenMousePos;
                        _initialWindowRect = _recipeResultPageRect;
                        currentEvent.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (_isResizing)
                    {
                        Vector2 currentMousePos = screenMousePos;
                        float newWidth = Mathf.Max(_minWindowSize.x, _initialWindowRect.width + (currentMousePos.x - _initialMousePosition.x));
                        float newHeight = Mathf.Max(_minWindowSize.y, _initialWindowRect.height + (currentMousePos.y - _initialMousePosition.y));

                        _recipeResultPageRect.width = newWidth;
                        _recipeResultPageRect.height = newHeight;
                        currentEvent.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (_isResizing)
                    {
                        _isResizing = false;
                        currentEvent.Use();
                    }
                    break;
            }
        }

        [System.Serializable]
        private class WindowPositions
        {
            public float productListX;
            public float productListY;
            public float favsListX;
            public float favsListY;
            public float recipeResultX;
            public float recipeResultY;
            public float priceFilterX;
            public float priceFilterY;
            public float recommendedWindowX;
            public float recommendedWindowY;

             
            public float recipeResultWidth;
            public float recipeResultHeight;
        }
        private static void SaveWindowPositions()
        {
             
            Dictionary<string, float> positions = new Dictionary<string, float>
    {
        { "productListX", _productListPageRect.x },
        { "productListY", _productListPageRect.y },
        { "favsListX", _favsListPageRect.x },
        { "favsListY", _favsListPageRect.y },
        { "recipeResultX", _recipeResultPageRect.x },
        { "recipeResultY", _recipeResultPageRect.y },
        { "priceFilterX", _priceFilterWindowRect.x },
        { "priceFilterY", _priceFilterWindowRect.y },
        { "recommendedWindowX", _recommendedWindowRect.x },
        { "recommendedWindowY", _recommendedWindowRect.y },
        { "recipeResultWidth", _recipeResultPageRect.width },
        { "recipeResultHeight", _recipeResultPageRect.height }
    };

             
            string filePath = Path.Combine(MelonEnvironment.GameRootDirectory, "Mods", "RecipeListGUI", "window_positions.txt");

             
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

             
            List<string> lines = new List<string>();
            foreach (var pair in positions)
            {
                lines.Add($"{pair.Key}={pair.Value}");
            }

            File.WriteAllLines(filePath, lines);
            Melon<RecipeListGuiClass>.Logger.Msg("Window positions saved");
        }


        private static void LoadWindowPositions()
        {
            string filePath = Path.Combine(MelonEnvironment.GameRootDirectory, "Mods", "RecipeListGUI", "window_positions.txt");

            if (File.Exists(filePath))
            {
                try
                {
                     
                    string[] lines = File.ReadAllLines(filePath);
                    Dictionary<string, float> positions = new Dictionary<string, float>();

                     
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            float parsedValue;
                            if (float.TryParse(parts[1], out parsedValue))
                            {
                                positions[parts[0]] = parsedValue;
                            }
                        }
                    }

                     
                    float productListX;
                    if (positions.TryGetValue("productListX", out productListX))
                        _productListPageRect.x = productListX;

                    float productListY;
                    if (positions.TryGetValue("productListY", out productListY))
                        _productListPageRect.y = productListY;

                    float favsListX;
                    if (positions.TryGetValue("favsListX", out favsListX))
                        _favsListPageRect.x = favsListX;

                    float favsListY;
                    if (positions.TryGetValue("favsListY", out favsListY))
                        _favsListPageRect.y = favsListY;

                    float recipeResultX;
                    if (positions.TryGetValue("recipeResultX", out recipeResultX))
                        _recipeResultPageRect.x = recipeResultX;

                    float recipeResultY;
                    if (positions.TryGetValue("recipeResultY", out recipeResultY))
                        _recipeResultPageRect.y = recipeResultY;

                    float priceFilterX;
                    if (positions.TryGetValue("priceFilterX", out priceFilterX))
                        _priceFilterWindowRect.x = priceFilterX;

                    float priceFilterY;
                    if (positions.TryGetValue("priceFilterY", out priceFilterY))
                        _priceFilterWindowRect.y = priceFilterY;

                    float recommendedWindowX;
                    if (positions.TryGetValue("recommendedWindowX", out recommendedWindowX))
                        _recommendedWindowRect.x = recommendedWindowX;

                    float recommendedWindowY;
                    if (positions.TryGetValue("recommendedWindowY", out recommendedWindowY))
                        _recommendedWindowRect.y = recommendedWindowY;

                    float recipeResultWidth;
                    if (positions.TryGetValue("recipeResultWidth", out recipeResultWidth))
                        _recipeResultPageRect.width = recipeResultWidth;

                    float recipeResultHeight;
                    if (positions.TryGetValue("recipeResultHeight", out recipeResultHeight))
                        _recipeResultPageRect.height = recipeResultHeight;

                    Melon<RecipeListGuiClass>.Logger.Msg("Window positions loaded");
                }
                catch (Exception ex)
                {
                    Melon<RecipeListGuiClass>.Logger.Error($"Error loading window positions: {ex.Message}");
                     
                    ResetWindowPositions();
                }
            }
            else
            {
                Melon<RecipeListGuiClass>.Logger.Msg("No saved window positions found, using defaults");
                 
                ResetWindowPositions();
            }
        }

        private static void ResetWindowPositions()
        {
            _productListPageRect = new Rect(100, 20, 350, 400);
            _shouldMinimizeProductListPage = false;
            _favsListPageRect = new Rect(100, 325, 350, 400);
            _shouldMinimizeFavListPage = false;
            _recipeResultPageRect = new Rect(600, 20, 600, 600);
            _priceFilterWindowRect = new Rect(400, 20, 350, 180);
            _recommendedWindowRect = new Rect(400, 220, 400, 400);
        }

        public override void OnApplicationQuit()
        {
            if (_guiShowen)
            {
                SaveWindowPositions();
            }
        }
    }
}

