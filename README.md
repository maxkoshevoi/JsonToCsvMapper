# JsonToCsvMapper
Powerful and versatile json to csv mapper

## Mapper
### Creating mapper
To create mapper simply pass mapping and filepath to it. You also can specify `fieldSeperator` and if you want to `printHeaders` on first line of the file.

```cs
   Mapper mainMapper = new(GetCatalogfileMapping(), Path.Combine(catalogfilePath, "Catalog.txt"));
```
### Using mapper

- You can print column headers manually at any time using `PrintHeaders()` method (e.g. if you want to print them at the end of the file or after every N rows).
- To map new JSON entity to CSV call `AppendEntity(JObject entity)` method.

## Mapping tutorial

Let’s say we are running e-shop. There is some API that we have no control over. It returns information about products available to purchase from our wholesaler. But our system only accepts CSV as an import format (for better human readability or other reasons).

### Part 1: basics, `BuildMapping` and `BuildConstant` methods

So, we have following JSON with product information:

```json
{
    "PrimaryID": "000574",
    "supplierNumber": "9",
    "tradePrice": "15.68",
    "sellPackWeightG": "105",
    "description": "Some great product",
    "countryOfOrigin": "Mexico"
}
```

And we want to generate CSV file with product catalog:

```csv
Product Code|Description|Alias|TradePrice|SellPackWeight (kg)|Country
000574|Some great product||15.68|0,105|Mexico
```

We can achieve that via this mapping:

```cs
private static OrderedDictionary GetCatalogfileMapping()
{
    OrderedDictionary mapping = new()
    {
        { "Product Code", MappingOptions.BuildMapping("PrimaryID", null, reportIfMissing: true) },
        { "Description", MappingOptions.BuildMapping("description") },
        { "Alias", MappingOptions.BuildConstant("") },
        { "TradePrice", MappingOptions.BuildMapping("tradePrice", "0") },
        { "SellPackWeight (kg)", MappingOptions.BuildMapping("sellPackWeightG", "0", (item) => (int.Parse(item) / 1000d).ToString()) },
        { "Country", MappingOptions.BuildMapping("countryOfOrigin") }
    };
    return mapping;
}
```

> So, mapping is `OrderedDictionary` that describes how to create single CSV file, where keys are CSV column names (could be empty, if you don't need column headers in output file) and values are instances of `MappingOptions` class (use `BuildXXX` method to create one)

##### Things to note about Mapping process:
- Only properties that are present in the mapping will be processed, others will be ignored (like `supplierNumber` in the example above).
- Supported property types are primitive values and arrays of primitive values.

##### Things to note about `BuildConstant`:
- In case JSON entity doesn't contain some information that is needed in output CSV file, we can use `BuildConstant` to hard code it. E.g. `Alias` property in the example above.

##### Things to note about `BuildMapping`:
1. `BuildMapping` accepts several arguments. First one is `JSON property name` value of which will be taken (it could be primitive value or array). If you want to write values from several JSON properties into single CSV column, use `BuildMappingList` method.
2. Next is `defaultValue`. This is placeholder in case if value (or property itself) is not found. 
If you enter **null** as default value, whole JSON entity will be skipped if value of this property won't be found. E.g. if there is no `PrimaryID` property in JSON file above, nothing will be written in output CSV file, but if there is no `TradePrice` property, in output CSV it's value will be **0**.
3. `action`. It's action that is performed on property value before writing it to output.
E.g. in our JSON file weight is in grams, but we need it in kilograms in our CSV file, so we've added small converter for that.
Also, this argument can be used for converting *Date* or *Boolean* vales to different format and many other things.
4. `arrayItemIndex`. It's not used in example above, but will be demonstrated later. If property value is an array, `arrayItemIndex` specifies which array item needs to be written to the output.
If you enter **null**, all array items will be written (one per row). So if you have a property which is array with 3 items in it, and you'll write **null** as `arrayItemIndex`, than there will be three rows as an output, where all values are the same except one from this property. There will be example for this later.
5. And the last one: `reportIfMissing`. `Mapper` class has `PropertyMissing` event, it will be fired if property with this attribute set to **true** is missing.

### Part 2: name-value files, `BuildMappingList` and `BuildConstantList` methods

Let's explore our JSON further. Product catalog is good, but each product can have different attributes (*like size, color, type*) and different types of products can have different attributes (e.g. pen can have *Re-fillable* attribute, while paper doesn't have it).
We cannot save them in our catalog file, because there will be a lot of empty cells and file will be too large.
So, let's turn our file structure 90 degrease and create file with three columns (`ProductID`, `Name`, `Value`) to contain our product attributes.

JSON:

```json
{
    "PrimaryID": "000574",
    "Dimensions": "210x297mm",
    "Type": "Ballpoint & Rollerball Pens",
    "Manufacturer": "Newell Rubbermaid",
    "Color": "Blue",
    "On Promotion": "No",
    "Recycled Percentage": "0.55"
}
```

Resulting CSV:

```csv
ProductID|Name|Value|Sequence
000574|Type|Ballpoint & Rollerball Pens|1
000574|Manufacturer|Newell Rubbermaid|1
000574|Color|Blue|1
000574|On Promotion|No|1
000574|Dimensions|210x297mm|1
000574|Recycled Percentage|0.55|1
```

Mapping:

```cs
private static OrderedDictionary GetAttributesMapping()
{
    OrderedDictionary mapping = new()
    {
        { "ProductID", MappingOptions.BuildMapping("PrimaryID") },
        { "Name", MappingOptions.BuildConstantList(new List<string>()
            {
                "Type", "Manufacturer", "Color", "Size", "On Promotion", "Dimensions", "Weight", "Eco-Aware", "Warranty",
                "Recycled Percentage", "Recyclable", "Biodegradable", "Re-fillable",
            })
        },
        { "Value", MappingOptions.BuildMappingList(new List<string>()
            {
                "Type", "Manufacturer", "Color", "Size", "On Promotion", "Dimensions", "Weight", "Eco-Aware", "Warranty",
                "Recycled Percentage", "Recyclable", "Biodegradable", "Re-fillable",
            }, null)
        },
        { "Sequence", MappingOptions.BuildConstant("1") }, // For sorting
    };
    return mapping;
}
```

> I've added `Sequence` column to demonstrate usage `BuildConstant` in combination with `BuildConstantList` and `BuildMappingList`

Let's summarize:
- `BuildConstantList` - Writes multiple predefined values in rows of a single column.
- `BuildMappingList` - Writes values (can be arrays) of multiple properties in rows of a single column. It accepts same arguments as `BuildMapping`.
- All 4 build methods (`BuildConstant`, `BuildMapping`, `BuildConstantList` and `BuildMappingList`) can be used within the same mapping as demonstrated in example above.
- Notice that `defaultValue` for `Value` column is set to **null**. So, if product doesn't have some attribute, row with it will be skipped. This way we can specify all possible attributes in mapping and only ones that are present will be written to output file.

### Part 3: arrays and `arrayItemIndex` = **null**

Ok, so hardcore stuff here.
In addition to general and type-specific attributes our product has attachments: images and manuals. There could be any number (including zero) of those. Information about attachments spread across several arrays (first contains urls, second - filetypes, third - sort order), and in our store images and manuals are stored in the same place, therefore it is required that images had **IMG** filetype (they will be displayed on product page as images), and files with any other types will be shown as downloadable links.

JSON:

```json
{
    "PrimaryID": "000574",
    "imagesType": [
        "JPEG",
        "PNG"
    ],
    "imageURL": [
        "https://example.com/image000574-1.jpg",
        "https://example.com/image000574-2.png"
    ],
    "imageSortOrder": [
        1,
        2
    ],
    "datasheetType": [
        "PDF"
    ],
    "datasheetURL": [
        "https://example.com/manual000574.pdf"
    ],
    "datasheetSortOrder": [
        1
    ],
}
```

Resulting CSV:

```csv
000574|IMG|1|https://example.com/image000574-1.jpg
000574|IMG|2|https://example.com/image000574-2.png
000574|PDF|1|https://example.com/manual000574.pdf
```
> You can choose whether to print the column headers in `Mapper` configuration or print them manually

Mapping:

```cs
private static OrderedDictionary GetMediaLinksMapping()
{
    OrderedDictionary mapping = new()
    {
        { "ProductId", MappingOptions.BuildMapping("PrimaryID") },
        { "MediaType", MappingOptions.BuildMappingList(
            new List<string> { "imagesType", "datasheetType" },
            action: (value) => {
                value = value.ToUpper();
                if (new string[] { "JPEG", "JPG", "PNG", "GIF" }.Contains(value))
                {
                    value = "IMG";
                }
                return value;
            }, arrayItemIndex: null)
        },
        { "Order", MappingOptions.BuildMappingList(new List<string> 
            { 
                "imageSortOrder", "datasheetSortOrder"
            }, arrayItemIndex: null)
        },
        { "Url", MappingOptions.BuildMappingList(new List<string> 
            { 
                "imageURL", "datasheetURL"
            }, null, arrayItemIndex: null)
        },
    };
    return mapping;
}
```

##### Things to note
- First of all, notice that we using `BuildMappingList` because we need to get values from two arrays (with images and manuals).
- We use `action` argument to specify which link contains image by replacing filetype with **IMG**.
- Notice that `defaultValue` for `Url` column is set to **null** to skip rows without url.
- And finally, `arrayItemIndex: null`. As you can see, in output file we have two rows with images and one with manual, that's because array with images contained two items. 
First, mapper created row with values from all properties that are not marked with `arrayItemIndex: null`, and left placeholder values for those properties that are. Then it duplicated this row and replaced placeholder values with array items.
- If you set `arrayItemIndex` to an integer, only item with that index will be taken (default value is **0**).
