using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.StationFramework;
using MelonLoader;
using UnityEngine;
using MelonLoader.Utils;
[assembly: MelonInfo(typeof(RecipeListGui.RecipeListGuiClass), "Recipe List", "1.0.3", "Rezx & ispa")]

namespace RecipeListGui
{
    public class RecipeListGuiClass : MelonMod
    {

        private class DataForFullIngredentsList
        {
            public string Name;
            public int Qnt;
        }

        // Словарь для перевода названий продуктов и ингредиентов (Dictionary for translating names of products and ingredients)
        private static Dictionary<string, string> _translationDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Функция перевода (The translation function)
        private static string Translate(string englishText)
        {
            if (string.IsNullOrEmpty(englishText))
                return englishText;

            if (_translationDictionary.TryGetValue(englishText, out string translation))
                return translation;

            return englishText; // Возвращаем оригинальный текст, если перевод не найден (We return the original text if the translation is not found)
        }

        // Загрузка переводов из файла (Downloading translations from a file)
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
                // Создаем директорию, если она не существует (Creating a directory if it does not exist)
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

        // Переменные для фильтрации по цене (Variables for filtering by price)
        private static bool _showPriceFilterWindow = false;
        private static Rect _priceFilterWindowRect = new Rect(400, 20, 350, 180);
        private static float _minPrice = 0f;
        private static float _maxPrice = 1000f;
        private static bool _isPriceFilterActive = false;
        private static bool _sortByPriceAscending = true;

        public override void OnInitializeMelon()
        {
            LoadTranslations();
            Melon<RecipeListGuiClass>.Logger.Msg("F5 to open");
            Melon<RecipeListGuiClass>.Logger.Msg("press F6 while gui is open to reset gui location");
        }

        public override void OnLateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F5))
            {
                ToggleMenu();
            }
            if (Input.GetKeyDown(KeyCode.F6) && _guiShowen)
            {
                MelonLogger.Msg("F6 pressed reset gui location");
                _productListPageRect = new Rect(100, 20, 295, 300);
                _shouldMinimizeProductListPage = false;
                _favsListPageRect = new Rect(100, 325, 295, 300);
                _shouldMinimizeFavListPage = false;
                _recipeResultPageRect = new Rect(600, 20, 600, 600);
                _priceFilterWindowRect = new Rect(400, 20, 300, 150);
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
                _listOfCreatedProducts = null;
                MelonEvents.OnGUI.Unsubscribe(DrawPages);
            }
        }


        private static void DrawPages()
        {

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

            // Добавляем окно фильтра цен, если оно активно (Adding a price filter window if it is active)
            if (_showPriceFilterWindow)
            {
                _priceFilterWindowRect = GUI.Window(654, _priceFilterWindowRect, (GUI.WindowFunction)PriceFilterWindow, Translate("Price Filter"), customWindowStyle);
            }
        }

        // Метод для окна фильтра цен (The method for the price filter window)
        private static void PriceFilterWindow(int windowId)
        {
            // Заголовок и описание (Title and description)
            GUI.Label(new Rect(20, 30, 310, 20), Translate("Enter price range to filter products:"));

            // Отображаем текущие значения (Displaying the current values)
            GUI.Label(new Rect(20, 60, 80, 20), Translate("Min Price:"));
            GUI.Label(new Rect(100, 60, 80, 20), $"${_minPrice}");

            GUI.Label(new Rect(20, 100, 80, 20), Translate("Max Price:"));
            GUI.Label(new Rect(100, 100, 80, 20), $"${_maxPrice}");

            // Кнопки для изменения минимальной цены (Buttons for changing the minimum price)
            if (GUI.Button(new Rect(180, 60, 30, 20), "-"))
            {
                _minPrice = Math.Max(0, _minPrice - 10);
            }
            if (GUI.Button(new Rect(215, 60, 30, 20), "+"))
            {
                _minPrice = Math.Min(_maxPrice, _minPrice + 10);
            }

            // Кнопки для больших изменений минимальной цены (Buttons for large minimum price changes)
            if (GUI.Button(new Rect(250, 60, 40, 20), "-100"))
            {
                _minPrice = Math.Max(0, _minPrice - 100);
            }
            if (GUI.Button(new Rect(295, 60, 40, 20), "+100"))
            {
                _minPrice = Math.Min(_maxPrice, _minPrice + 100);
            }

            // Кнопки для изменения максимальной цены (Buttons for changing the maximum price)
            if (GUI.Button(new Rect(180, 100, 30, 20), "-"))
            {
                _maxPrice = Math.Max(_minPrice, _maxPrice - 10);
            }
            if (GUI.Button(new Rect(215, 100, 30, 20), "+"))
            {
                _maxPrice += 10;
            }

            // Кнопки для больших изменений максимальной цены (Buttons for large changes of the maximum price)
            if (GUI.Button(new Rect(250, 100, 40, 20), "-100"))
            {
                _maxPrice = Math.Max(_minPrice, _maxPrice - 100);
            }
            if (GUI.Button(new Rect(295, 100, 40, 20), "+100"))
            {
                _maxPrice += 100;
            }

            // Кнопка применения фильтра (Filter application button)
            if (GUI.Button(new Rect(20, 140, 150, 25), Translate("Apply Filter")))
            {
                _isPriceFilterActive = true;
                _showPriceFilterWindow = false;

                // Сбрасываем скролл, чтобы показать начало отфильтрованного списка (We reset the scroll to show the beginning of the filtered list.)
                _productListPageScrollViewVector = Vector2.zero;
            }

            // Кнопка сброса фильтра (Filter reset button)
            if (GUI.Button(new Rect(180, 140, 150, 25), Translate("Reset Filter")))
            {
                _isPriceFilterActive = false;
                _minPrice = 0f;
                _maxPrice = 1000f;
            }

            // Кнопка закрытия окна
            if (GUI.Button(new Rect(_priceFilterWindowRect.width - 30, 5, 25, 20), "X"))
            {
                _showPriceFilterWindow = false;
            }

            // Позволяем перемещать окно
            GUI.DragWindow(new Rect(0, 0, _priceFilterWindowRect.width, 25));
        }

        private static Il2CppSystem.Collections.Generic.List<ProductDefinition> GetlistOfCreatedProducts()
        {
            GameObject productObject = GameObject.Find("@Product");
            if (productObject == null)
            {
                //Printy("Product object not found");
                return null;
            }
            ProductManager productManagerComp = productObject.GetComponent<ProductManager>();
            if (productManagerComp == null)
            {
                //Printy("ProductManager component not found");
                return null;
            }
            return productManagerComp.AllProducts;
        }


        private static Il2CppSystem.Collections.Generic.List<ProductDefinition> GetlistOf_FavProducts()
        {
            GameObject productObject = GameObject.Find("@Product");
            if (productObject == null)
            {
                //Printy("Product object not found");
                return null;
            }
            ProductManager productManagerComp = productObject.GetComponent<ProductManager>();
            if (productManagerComp == null)
            {
                //Printy("ProductManager component not found");
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
            //Printy($"Selected {product.name} it has {product.Recipes.Count} recipes.");
            ProcessProduct(product, outputLines, selectedProductRecipe, true, ingredientListTemp);

            outputLines.Add("-----------------------------------------------------------------------------------");
            outputLines.Add($"{Translate("Market Price")} {product.MarketValue}, {Translate("Additives")}: {Math.Round(product.GetAddictiveness())}, {Translate("Cost")}: {_costToMake}, {Translate("Final Markup")}: {product.MarketValue - _costToMake} ({Translate("base ingredients not included")})");
            outputLines.Add("-----------------------------------------------------------------------------------");

            foreach (var ingredient in ingredientListTemp)
            {
                outputLines.Add($"{ingredient.Qnt}x {Translate(ingredient.Name)} ");
            }
            return outputLines;
        }

        static void ProcessProduct(ProductDefinition product, Il2CppSystem.Collections.Generic.List<string> outputLines, int recipeToUse, bool isSelectedProductRecipe, System.Collections.Generic.List<DataForFullIngredentsList> ingredientListTemp)
        {
            //Printy($"ProcessProduct recipeToUse {recipeToUse}");

            if (!isSelectedProductRecipe && product == _selectedBud)
            {
                return;
            }

            GameObject productObject = GameObject.Find("@Product");
            if (productObject == null)
            {
                //Printy("ProcessProduct: Product object not found");
                return;
            }
            ProductManager productManagerComp = productObject.GetComponent<ProductManager>();
            if (productManagerComp == null)
            {
                //Printy("ProcessProduct: ProductManager component not found");
                return;
            }

            if (product.Recipes.Count >= 1 && !productManagerComp.DefaultKnownProducts.Contains(product))
            {
                var recipes = product.Recipes;

                StationRecipe recipe = recipes.ToArray().ToList()[recipeToUse]; // use the selected recipe 

                if (isSelectedProductRecipe) // check if the recipe is the for the selected product
                {
                    outputLines.Add($"{Translate("Recipe for")} ({Translate(recipe.RecipeTitle)}) ");
                    string effects = "";
                    foreach (var effect in product.Properties)
                    {
                        if (effects == "")
                        {
                            effects = $"{Translate(effect.Name)}";
                        }
                        else
                        {
                            effects = $"{effects}, {Translate(effect.Name)}";
                        }
                    }
                    outputLines.Add(effects);
                    outputLines.Add($"-----------------------------------------------------------------------------------");

                    isSelectedProductRecipe = false;
                }
                else
                {
                    outputLines.Add($"{Translate("Recipe")}: {Translate(recipe.RecipeTitle)}");
                }
                // list for products with their own recipe
                Il2CppSystem.Collections.Generic.List<ProductDefinition> nestedProducts = new Il2CppSystem.Collections.Generic.List<ProductDefinition>();

                foreach (var ingredient in recipe.Ingredients)
                {
                    string cat = ingredient.Item.Category.ToString();

                    if (cat == "Consumable" || cat == "Ingredient")
                    {
                        StorableItemDefinition prop = ingredient.Item.TryCast<StorableItemDefinition>();
                        //PropertyItemDefinition prop = ingredient.Item as PropertyItemDefinition;}

                        if (prop != null)
                        {
                            outputLines.Add($"{ingredient.Quantity}x {Translate(prop.Name)} {prop.BasePurchasePrice}$");
                            // list for doing ingredient qnt count
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
                        // For product ingredients, print the ingredient line first.
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
                            // If this product has a recipe store it to process after all normal ingredients
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

                // after listing all ingredients for current product repeat for any drug products found in ingredient list.
                foreach (var nestedProduct in nestedProducts)
                {
                    ProcessProduct(nestedProduct, outputLines, 0, false, ingredientListTemp); // pass 0 for recipe to use because the nested isnt the selected dumbass
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

            // close button
            if (GUI.Button(new Rect(_recipeResultPageRect.width - 45, 20, 30, 30), "X"))
            {
                _selectedBud = null;
                _hasSelectedBud = false;
                return;
            }
            // if product has more then 1 recipe make a list otherwise just load the only recipe
            //_selectedProductRecipeIndex is set to 0 when a new product is selected
            if (_selectedBud.Recipes.Count > 1)
            {
                if (_hasSelectedProductRecipe)
                {
                    // Back button
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
                // get new ingredientList when selection changes.
                if (_ingredientListRecipePage == null || _selectedBud != _lastSelectedBud)
                {
                    _ingredientListRecipePage = GetIngredientList(_selectedBud, _selectedProductRecipeIndex);

                    _lastSelectedBud = _selectedBud;
                }

                _recipeResultPageScrollViewVector = GUI.BeginScrollView(new Rect(55, 20, _recipeResultPageRect.width - 55, _recipeResultPageRect.height), _recipeResultPageScrollViewVector, new Rect(0, 0, _recipeResultPageRect.width - 55, _ingredientListRecipePage.Count * 30));

                // make lables with the info from GetIngredientList
                var ingredientListTemp = _ingredientListRecipePage.ToArray().ToList();
                for (int i = 0; i < _ingredientListRecipePage.Count; i++)
                {
                    GUI.Label(new Rect(10, 40 + (20 * i), _recipeResultPageRect.width - 75, 20), ingredientListTemp[i]);
                }
                GUI.EndScrollView();
            }


            // resize
            ProcessResize(resizeHandleRect);
            GUI.DragWindow(new Rect(0, 0, _recipeResultPageRect.width, _recipeResultPageRect.height - 30));
        }


        private static Vector2 _productListPageScrollViewVector = Vector2.zero;
        private static Rect _productListPageRect = new Rect(100, 20, 295, 55);
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

                    _productListPageRect = new Rect(100, 20, 295, 300);
                }
                GUI.DragWindow(new Rect(20, 10, 500, 500));
                return;
            }
            else
            {
                if (GUI.Button(new Rect(_productListPageRect.width - 25, 2, 17, 17), "-"))
                {
                    _shouldMinimizeProductListPage = true;

                    _productListPageRect = new Rect(100, 20, 295, 55);
                    return;
                }
            }

            _listOfCreatedProducts ??= GetlistOfCreatedProducts();
            if (_listOfCreatedProducts == null)
            {
                return;
            }


            if (!_hasSelectedProductType)
            {
                if (GUI.Button(new Rect(50, 20, 200, 20), Translate("Marijuana")))
                {
                    _hasSelectedProductType = true;
                    _typeOfDrugToFilter = "Marijuana";
                }
                if (GUI.Button(new Rect(50, 40, 200, 20), Translate("Methamphetamine")))
                {
                    _hasSelectedProductType = true;
                    _typeOfDrugToFilter = "Methamphetamine";
                }
                if (GUI.Button(new Rect(50, 60, 200, 20), Translate("Cocaine")))
                {
                    _hasSelectedProductType = true;
                    _typeOfDrugToFilter = "Cocaine";
                }
                if (GUI.Button(new Rect(50, 80, 200, 20), Translate("All Products")))
                {
                    _hasSelectedProductType = true;
                }

                // Добавляем кнопку фильтра по цене под другими кнопками
                if (GUI.Button(new Rect(50, 100, 200, 20), Translate("Filter by Price")))
                {
                    _showPriceFilterWindow = true;
                }


                GUI.DragWindow(new Rect(40, 10, 500, 500));

                if (!_hasSelectedProductType)
                {
                    return;
                }
            }
            else
            {
                // Back button
                if (GUI.Button(new Rect(_productListPageRect.width - 37, 20, 29, 27), "B"))
                {
                    _hasSelectedProductType = false;
                    _typeOfDrugToFilter = "";
                    _productListPageScrollViewVector = Vector2.zero;
                }

                // Когда выбран тип продукта, показываем кнопку фильтра рядом с кнопкой "Назад"
                if (GUI.Button(new Rect(_productListPageRect.width - 37, 50, 29, 27), "$"))
                {
                    _showPriceFilterWindow = true;
                }

                // Кнопка сортировки по цене
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

            // Добавляем фильтрацию по цене
            if (_isPriceFilterActive)
            {
                sortedProducts = sortedProducts.Where(product =>
                    product.MarketValue >= _minPrice && product.MarketValue <= _maxPrice).ToList();
            }

            // Применяем сортировку по цене
            if (_sortByPriceAscending)
            {
                sortedProducts = sortedProducts.OrderBy(product => product.MarketValue).ToList();
            }
            else
            {
                sortedProducts = sortedProducts.OrderByDescending(product => product.MarketValue).ToList();
            }

            _productListPageScrollViewVector = GUI.BeginScrollView(new Rect(55, 20, 300, 300), _productListPageScrollViewVector, new Rect(0, 0, 300, sortedProducts.Count * 20 + 10));

            // i should probably not render buttons that are offscreen or do a page system but 2 brain cells
            int spacer = 0;
            foreach (var createdProduct in sortedProducts)
            {
                if (GUI.Button(new Rect(0, 20 * spacer, 160, 20), Translate(createdProduct.name)))
                {
                    //Printy($"Selected {createdProduct.name}");
                    _selectedBud = createdProduct;
                    _hasSelectedBud = true;
                    _ingredientListRecipePage = null;
                    _hasSelectedProductRecipe = false;
                    _selectedProductRecipeIndex = 0;
                }

                // Отображаем цену рядом с названием продукта
                GUI.Label(new Rect(165, 20 * spacer, 50, 20), $"${createdProduct.MarketValue}");

                spacer++;
            }
            GUI.EndScrollView();
            GUI.DragWindow(new Rect(20, 10, 500, 500));
        }


        private static Vector2 _favsListPageScrollViewVector = Vector2.zero;
        private static Rect _favsListPageRect = new Rect(100, 325, 295, 55);
        private static Il2CppSystem.Collections.Generic.List<ProductDefinition>? _listOf_FavsProducts;
        private static bool _shouldMinimizeFavListPage = true;
        static void FavListPage(int windowID)
        {

            if (_shouldMinimizeFavListPage)
            {
                if (GUI.Button(new Rect(_favsListPageRect.width - 25, 2, 18, 17), "+"))
                {
                    _shouldMinimizeFavListPage = false;

                    _favsListPageRect = new Rect(100, 325, 295, 300);
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

                    _favsListPageRect = new Rect(100, 325, 295, 55);
                    return;
                }
            }

            _listOf_FavsProducts ??= GetlistOf_FavProducts();
            if (_listOf_FavsProducts == null)
            {
                //Printy("listOf_FavsProducts is null");
                return;
            }

            // Применяем фильтрацию по цене к избранным продуктам, если фильтр активен
            var filteredFavProducts = _listOf_FavsProducts.ToArray().ToList();
            if (_isPriceFilterActive)
            {
                filteredFavProducts = filteredFavProducts.Where(product =>
                    product.MarketValue >= _minPrice && product.MarketValue <= _maxPrice).ToList();
            }

            // Применяем сортировку по цене
            if (_sortByPriceAscending)
            {
                filteredFavProducts = filteredFavProducts.OrderBy(product => product.MarketValue).ToList();
            }
            else
            {
                filteredFavProducts = filteredFavProducts.OrderByDescending(product => product.MarketValue).ToList();
            }

            _favsListPageScrollViewVector = GUI.BeginScrollView(new Rect(55, 20, 300, 500), _favsListPageScrollViewVector, new Rect(0, 0, 300, filteredFavProducts.Count * 20 + 10));

            int index = 0;
            foreach (var favProduct in filteredFavProducts)
            {
                if (GUI.Button(new Rect(0, 20 * index, 160, 20), Translate(favProduct.name)))
                {
                    //Printy($"Selected Fav: {favProduct.name}");
                    _selectedBud = favProduct;
                    _hasSelectedBud = true;
                    _ingredientListRecipePage = null;
                    _hasSelectedProductRecipe = false;
                    _selectedProductRecipeIndex = 0;
                }

                // Отображаем цену рядом с названием продукта
                GUI.Label(new Rect(165, 20 * index, 50, 20), $"${favProduct.MarketValue}");

                index++;
            }
            GUI.EndScrollView();
            GUI.DragWindow(new Rect(20, 10, 500, 500));
        }


        // ProcessResize was ai generated because 2 brain cells
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

        /*
        private static void Printy(string text)
        {
            Melon<RecipeListGuiClass>.Logger.Msg(text);
        }
        */
    }
}


