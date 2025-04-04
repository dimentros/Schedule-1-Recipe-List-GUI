using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.StationFramework;
using MelonLoader;
using UnityEngine;
[assembly: MelonInfo(typeof(RecipeListGui.RecipeListGuiClass), "Recipe List", "1.0.2", "Rezx")]

namespace RecipeListGui
{
    public class RecipeListGuiClass : MelonMod
    {
        
        private class DataForFullIngredentsList
        {
            public string Name;
            public int Qnt;
        }
        
        public override void OnInitializeMelon()
        {
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
            }
        }

        
        private static bool _guiShowen;
        private static void ToggleMenu()
        {
            _guiShowen = !_guiShowen;
            if (_guiShowen)
            {
                MelonEvents.OnGUI.Subscribe(DrawPages, 50);
            }else {
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

            _productListPageRect = GUI.Window(651, _productListPageRect, (GUI.WindowFunction)ProductListPage, "Product List",customWindowStyle);
            _favsListPageRect = GUI.Window(652, _favsListPageRect, (GUI.WindowFunction)FavListPage, "Favorite  List",customWindowStyle);
            if (_hasSelectedBud)
            {
                _recipeResultPageRect = GUI.Window(653, _recipeResultPageRect, (GUI.WindowFunction)RecipePage, "Recipe",customWindowStyle);
            }
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
        static Il2CppSystem.Collections.Generic.List<string> GetIngredientList(ProductDefinition product,int selectedProductRecipe)
        {
            Il2CppSystem.Collections.Generic.List<string> outputLines = new();
            List<DataForFullIngredentsList> ingredientListTemp = new();

            _costToMake = 0;
            //Printy($"Selected {product.name} it has {product.Recipes.Count} recipes.");
            ProcessProduct(product, outputLines,selectedProductRecipe,true,ingredientListTemp);
            
            outputLines.Add("-----------------------------------------------------------------------------------");
            outputLines.Add($"MarketValue {product.MarketValue}, Addic: {Math.Round(product.GetAddictiveness())}, Cost: {_costToMake}, after cost: {product.MarketValue - _costToMake} (base ingredients not included)");
            outputLines.Add("-----------------------------------------------------------------------------------");
            
            foreach (var ingredient in ingredientListTemp)
            {
                outputLines.Add($"{ingredient.Qnt}x {ingredient.Name} ");

            }
            return outputLines;
        }
        
        
        static void ProcessProduct(ProductDefinition product,Il2CppSystem.Collections.Generic.List<string> outputLines,int recipeToUse, bool isSelectedProductRecipe,  System.Collections.Generic.List<DataForFullIngredentsList> ingredientListTemp)
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
                    outputLines.Add($"Recipe For ({recipe.RecipeTitle}) ");
                    string effects = "";
                    foreach (var effect in product.Properties)
                    {
                        if (effects == "")
                        {
                            effects = $"{effect.Name}";
                        }else {
                            effects = $"{effects}, {effect.Name}";
                        }
                    }
                    outputLines.Add(effects);
                    outputLines.Add($"-----------------------------------------------------------------------------------");
                    
                    isSelectedProductRecipe = false;
                }else {   
                    outputLines.Add($"Recipe: {recipe.RecipeTitle}");
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
                            outputLines.Add($"{ingredient.Quantity}x {prop.Name} {prop.BasePurchasePrice}$");
                            // list for doing ingredient qnt count
                            var existingIngredient = ingredientListTemp.ToArray().ToList().FirstOrDefault(i => i.Name == prop.Name); 
                            if (existingIngredient != null)
                            {
                                existingIngredient.Qnt += ingredient.Quantity;
                            }else {
                                ingredientListTemp.Add(new DataForFullIngredentsList() { Name = prop.Name, Qnt = ingredient.Quantity });
                            }
                            
                            _costToMake += prop.BasePurchasePrice * ingredient.Quantity;
                                
                        }else {
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
                                outputLines.Add($"{ingredient.Quantity}x {drug.Name}, Seed: {drug.BasePrice}$");

                            }else {
                                outputLines.Add($"{ingredient.Quantity}x {drug.Name}");
                            }
                            // If this product has a recipe store it to process after all normal ingredients
                            if (drug.Recipes.Count >= 1)
                            {
                                nestedProducts.Add(drug);
                            }
                        }else {
                            outputLines.Add($"{ingredient.Quantity}xIngredient: {ingredient.Item}");
                            Melon<RecipeListGuiClass>.Logger.Msg($"ERROR ProcessProduct: Drug null {cat}");
                        }
                    }else {
                        outputLines.Add($"{ingredient.Quantity}xIngredient: {ingredient.Item}");
                        Melon<RecipeListGuiClass>.Logger.Msg($"ERROR ProcessProduct: Unknown Category {cat}");
                    }
                }

                // after listing all ingredients for current product repeat for any drug products found in ingredient list.
                foreach (var nestedProduct in nestedProducts)
                {
                    ProcessProduct(nestedProduct, outputLines, 0,false,ingredientListTemp); // pass 0 for recipe to use because the nested isnt the selected dumbass
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
            Rect resizeHandleRect = new Rect(_recipeResultPageRect.width -50, _recipeResultPageRect.height -50, 25, 25);
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
                if(_hasSelectedProductRecipe)
                {
                    // Back button
                    if (GUI.Button(new Rect(_recipeResultPageRect.width - 85, 20, 30, 30), "B"))
                    {
                        _hasSelectedProductRecipe = false;
                        _ingredientListRecipePage = null;
                    }
                }
 

                if (!_hasSelectedProductRecipe)
                {
                    for (int i = 0; i < _selectedBud.Recipes.Count; i++)
                    {
                        if (GUI.Button(new Rect(_recipeResultPageRect.width/3, 50 + 20 * i, 200, 20), $"Recipe {i + 1}"))
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
                    _ingredientListRecipePage = GetIngredientList(_selectedBud,_selectedProductRecipeIndex);

                    _lastSelectedBud = _selectedBud;
                }
                
                _recipeResultPageScrollViewVector = GUI.BeginScrollView(new Rect(55, 20, _recipeResultPageRect.width - 55,  _recipeResultPageRect.height), _recipeResultPageScrollViewVector, new Rect(0, 0, _recipeResultPageRect.width - 55, _ingredientListRecipePage.Count * 30));
                
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
            }else {
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

                if (GUI.Button(new Rect(50, 20, 200, 20), "Marijuana"))
                {
                    _hasSelectedProductType = true;
                    _typeOfDrugToFilter = "Marijuana";
                }
                if (GUI.Button(new Rect(50, 40 , 200, 20), "Methamphetamine"))
                {
                    _hasSelectedProductType = true;
                    _typeOfDrugToFilter = "Methamphetamine";
                }
                if (GUI.Button(new Rect(50, 60, 200, 20), "Cocaine"))
                {
                    _hasSelectedProductType = true;
                    _typeOfDrugToFilter = "Cocaine";
                }
                if (GUI.Button(new Rect(50, 80, 200, 20), "All"))
                {
                    _hasSelectedProductType = true;
                }
                GUI.DragWindow(new Rect(40, 10, 500, 500));
                
                if (!_hasSelectedProductType)
                {
                    return;
                }
            }else {
                // Back button
                if (GUI.Button(new Rect(_productListPageRect.width - 37, 20, 29, 27), "B"))
                {
                    _hasSelectedProductType = false;
                    _typeOfDrugToFilter = "";
                    _productListPageScrollViewVector = Vector2.zero;
                }
            }
            

            
            var sortedProducts = _listOfCreatedProducts.ToArray().ToList();
            if (_typeOfDrugToFilter != "")
            {
                sortedProducts = sortedProducts.Where(product => product.DrugType.ToString() == _typeOfDrugToFilter).ToList();
            }
            
            _productListPageScrollViewVector = GUI.BeginScrollView(new Rect(55, 20, 300, 300), _productListPageScrollViewVector, new Rect(0, 0, 300, sortedProducts.Count * 20 + 10));

            // i should probably not render buttons that are offscreen or do a page system but 2 brain cells
            int spacer = 0;
            foreach (var createdProduct in sortedProducts)
            {
                if (GUI.Button(new Rect(0, 20 * spacer, 200, 20), createdProduct.name))
                {
                    //Printy($"Selected {createdProduct.name}");
                    _selectedBud = createdProduct;
                    _hasSelectedBud = true;
                    _ingredientListRecipePage = null;
                    _hasSelectedProductRecipe = false;
                    _selectedProductRecipeIndex = 0;
                }

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

            }else {
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
            
            _favsListPageScrollViewVector = GUI.BeginScrollView(new Rect(55, 20, 300, 300), _favsListPageScrollViewVector, new Rect(0, 0, 300, _listOf_FavsProducts.Count * 20 + 10));            
            int index = 0;
            foreach (var favProduct in _listOf_FavsProducts)
            {
                if (GUI.Button(new Rect(0, 20 * index, 200, 20), favProduct.name))
                {
                    //Printy($"Selected Fav: {favProduct.name}");
                    _selectedBud = favProduct;
                    _hasSelectedBud = true;
                    _ingredientListRecipePage = null;
                    _hasSelectedProductRecipe = false;
                    _selectedProductRecipeIndex = 0;
                }

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

