using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PlantDataBaseGenerator
{
    private const string DatabasePath = "Assets/Resources/Data/PlantDataBase.asset";

    [MenuItem("Treegimon/Plants/Generate Plant Database")]
    public static void GeneratePlantDatabase()
    {
        PlantDataBase database = AssetDatabase.LoadAssetAtPath<PlantDataBase>(DatabasePath);

        if (database == null)
        {
            Debug.LogError($"No se encontró PlantDataBase en la ruta: {DatabasePath}");
            return;
        }

        SerializedObject serializedDatabase = new SerializedObject(database);

        SerializedProperty plantsProperty = serializedDatabase.FindProperty("plants");
        SerializedProperty variantsProperty = serializedDatabase.FindProperty("modelVariants");

        if (plantsProperty == null || variantsProperty == null)
        {
            Debug.LogError("No se encontraron las listas internas 'plants' o 'modelVariants' en PlantDataBase.");
            return;
        }

        plantsProperty.ClearArray();
        variantsProperty.ClearArray();

        CreateSpeciesAssetsAndAssign(plantsProperty);
        AddModelVariants(variantsProperty);

        serializedDatabase.ApplyModifiedProperties();

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("PlantDataBase generado correctamente.");
    }

    private static void CreateSpeciesAssetsAndAssign(SerializedProperty plantsProperty)
    {
        List<PlantSpeciesData> species = new List<PlantSpeciesData>
        {
            CreateOrUpdateSpecies("alcaparro_enano", "Alcaparro enano", "Senna multiglandulosa", PlantBiomeType.Xerofito, GetRarity("Epic"), 1250, "Espinas", "Corteza resistente", "Adaptación extrema"),
            CreateOrUpdateSpecies("aliso", "Aliso", "Alnus acuminata", PlantBiomeType.Hidro, GetRarity("Epic"), 1250, "Corriente de agua", "Raíces hidratadas", "Fijación de nitrógeno"),
            CreateOrUpdateSpecies("suculentas", "Suculentas", "Suculentas", PlantBiomeType.Xerofito, GetRarity("Legendary"), 1500, "Espinas", "Reserva de agua", "Sequía adaptativa"),
            CreateOrUpdateSpecies("cedrillo", "Cedrillo", "Smallanthus pyramidalis", PlantBiomeType.Hidro, GetRarity("Legendary"), 1500, "Corriente de agua", "Raíces hidratadas", "Restauración ecológica"),
            CreateOrUpdateSpecies("cedro", "Cedro", "Cedrela montana", PlantBiomeType.Templado, GetRarity("Legendary"), 1500, "Crecimiento", "Equilibrio natural", "Restauración ecológica"),
            CreateOrUpdateSpecies("drago", "Drago", "Croton coriaceus", PlantBiomeType.Solar, GetRarity("Epic"), 1250, "Rayo solar", "Fotosíntesis", "Resina ardiente"),
            CreateOrUpdateSpecies("duraznillo", "Duraznillo", "Abatia parviflora", PlantBiomeType.Montana, GetRarity("Legendary"), 1500, "Golpe de tronco", "Raíces profundas", "Flor andina"),
            CreateOrUpdateSpecies("espino", "Espino", "Duranta mutisii", PlantBiomeType.Solar, GetRarity("Legendary"), 1500, "Rayo solar", "Fotosíntesis", "Defensa espinosa"),
            CreateOrUpdateSpecies("mangle", "Mangle", "Escallonia pendula", PlantBiomeType.Templado, GetRarity("Epic"), 1250, "Crecimiento", "Equilibrio natural", "Adaptación ambiental"),
            CreateOrUpdateSpecies("manzano", "Manzano", "Billia rosea", PlantBiomeType.Templado, GetRarity("Common"), 1000, "Crecimiento", "Equilibrio natural", "Fruto nutritivo"),
            CreateOrUpdateSpecies("nogal", "Nogal", "Juglans neotropica", PlantBiomeType.Montana, GetRarity("Epic"), 1250, "Golpe de tronco", "Raíces profundas", "Sombra del bosque"),
            CreateOrUpdateSpecies("pasto", "Pasto", "Pasto", PlantBiomeType.Templado, GetRarity("Common"), 900, "Hoja cortante", "Resistencia natural", "Brote rápido"),
            CreateOrUpdateSpecies("pino_romeron", "Pino romerón", "Retrophyllum rospigliosii", PlantBiomeType.Montana, GetRarity("Common"), 1000, "Golpe de tronco", "Raíces profundas", "Bosque antiguo"),
            CreateOrUpdateSpecies("roble", "Roble", "Quercus humboldtii", PlantBiomeType.Montana, GetRarity("Common"), 1000, "Golpe de tronco", "Raíces profundas", "Bosque protector"),
            CreateOrUpdateSpecies("sietecueros", "Sietecueros", "Tibouchina lepidota", PlantBiomeType.Templado, GetRarity("Epic"), 1250, "Crecimiento", "Equilibrio natural", "Floración abundante")
        };

        for (int i = 0; i < species.Count; i++)
        {
            plantsProperty.InsertArrayElementAtIndex(i);
            plantsProperty.GetArrayElementAtIndex(i).objectReferenceValue = species[i];
        }
    }

    private static PlantRarity GetRarity(string rarityName)
    {
        string[] candidates;

        switch (rarityName)
        {
            case "Common":
                candidates = new[] { "Common", "Comun", "Común", "Basica", "Básica", "Normal" };
                break;

            case "Epic":
                candidates = new[] { "Epic", "Epica", "Épica", "Rara", "Rare" };
                break;

            case "Legendary":
                candidates = new[] { "Legendary", "Legendaria", "Legendario", "Legend" };
                break;

            default:
                candidates = new[] { rarityName };
                break;
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            if (Enum.TryParse(candidates[i], true, out PlantRarity parsed))
                return parsed;
        }

        Array values = Enum.GetValues(typeof(PlantRarity));
        return values.Length > 0 ? (PlantRarity)values.GetValue(0) : default;
    }

    private static PlantSpeciesData CreateOrUpdateSpecies(
        string id,
        string displayName,
        string scientificName,
        PlantBiomeType biome,
        PlantRarity rarity,
        int hp,
        string basicAttack,
        string defense,
        string special)
    {
        string folderPath = "Assets/Resources/Data/PlantSpecies";

        if (!AssetDatabase.IsValidFolder(folderPath))
            AssetDatabase.CreateFolder("Assets/Resources/Data", "PlantSpecies");

        string assetPath = $"{folderPath}/Plant_{id}.asset";

        PlantSpeciesData species = AssetDatabase.LoadAssetAtPath<PlantSpeciesData>(assetPath);

        if (species == null)
        {
            species = ScriptableObject.CreateInstance<PlantSpeciesData>();
            AssetDatabase.CreateAsset(species, assetPath);
        }

        species.plantId = id;
        species.displayName = displayName;
        species.scientificName = scientificName;
        species.biomeType = biome;
        species.rarity = rarity;
        species.baseHP = hp;
        species.baseAttack = Mathf.RoundToInt(hp * 0.08f);
        species.baseDefense = 10;
        species.baseSpecialAttack = Mathf.RoundToInt(hp * 0.14f);
        species.defensePercent = 0.25f;
        species.minLevelToPvP = 1;

        species.xpWin = 150;
        species.xpLose = 100;
        species.xpWinBiomeBonus = 250;
        species.xpLoseBiomeBonus = 150;

        species.basicAttackDisplayName = basicAttack;
        species.defenseDisplayName = defense;
        species.specialDisplayName = special;

        species.combatRole = "Balanceado";
        species.generalDescription = $"Especie nativa: {displayName}.";
        species.careDescription = "Requiere cuidado según su bioma y estado de crecimiento.";
        species.obtainMethod = "Desbloqueada mediante progreso del usuario o inventario del archivo .tree.";

        EditorUtility.SetDirty(species);
        return species;
    }

    private static void AddModelVariants(SerializedProperty variantsProperty)
    {
        List<(string modelKey, string speciesId, string speciesName, string student)> variants = new()
        {
            ("Alcaparro_enano_Andres_Santiago_Duque_García", "alcaparro_enano", "Alcaparro enano", "Andres Santiago Duque García"),
            ("Alcaparro_enano_Jimmy_Alejandro_Torres_Heraque", "alcaparro_enano", "Alcaparro enano", "Jimmy Alejandro Torres Heraque"),
            ("Alcaparro_enano_Laura_Sofia_Parra_Ledezma", "alcaparro_enano", "Alcaparro enano", "Laura Sofia Parra Ledezma"),
            ("Alcaparro_enano_Maria_Paula_Hernandez_Hernandez", "alcaparro_enano", "Alcaparro enano", "Maria Paula Hernandez Hernandez"),
            ("Alcaparro_enano_Sara_Viviana_Rojas_Gómez", "alcaparro_enano", "Alcaparro enano", "Sara Viviana Rojas Gómez"),

            ("Aliso_Juan_Pablo_Yalta_Badillo", "aliso", "Aliso", "Juan Pablo Yalta Badillo"),
            ("Suculentas_Carlos_Bahamon", "suculentas", "Suculentas", "Carlos Bahamon"),

            ("Cedrillo_Geraldine_Torres_Reyes", "cedrillo", "Cedrillo", "Geraldine Torres Reyes"),
            ("Cedrillo_Jeronimo_Vargas_Hoyos", "cedrillo", "Cedrillo", "Jeronimo Vargas Hoyos"),
            ("Cedrillo_Tomas_Marín_Ojeda", "cedrillo", "Cedrillo", "Tomas Marín Ojeda"),

            ("Cedro_Juan_Manuel_Lombana_Cárdenas", "cedro", "Cedro", "Juan Manuel Lombana Cárdenas"),

            ("Drago_Adriana_Sofia_Espitia_Contreras", "drago", "Drago", "Adriana Sofia Espitia Contreras"),
            ("Drago_Alejandro_Ramirez_Velasquez", "drago", "Drago", "Alejandro Ramirez Velasquez"),
            ("Drago_Carlos_Ernesto_Correa_Rodríguez", "drago", "Drago", "Carlos Ernesto Correa Rodríguez"),
            ("Drago_David_Alejandro_Hernández_Prieto", "drago", "Drago", "David Alejandro Hernández Prieto"),
            ("Drago_Isabella_Vega_Heredia", "drago", "Drago", "Isabella Vega Heredia"),
            ("Drago_Jesica_Alejandra_Piñeros_Garcia", "drago", "Drago", "Jesica Alejandra Piñeros Garcia"),
            ("Drago_Tomas_Mateo_Buitrago_Gutiérrez", "drago", "Drago", "Tomas Mateo Buitrago Gutiérrez"),
            ("Drago_Yuri_Alexandra_Castañeda_Montaño", "drago", "Drago", "Yuri Alexandra Castañeda Montaño"),

            ("Duraznillo_Andres_Felipe_Morales_Domínguez", "duraznillo", "Duraznillo", "Andres Felipe Morales Domínguez"),
            ("Duraznillo_Anne_Catalina_Galvis_Carvajal", "duraznillo", "Duraznillo", "Anne Catalina Galvis Carvajal"),
            ("Duraznillo_Gireth_Sharik_Alvarado_Rubio", "duraznillo", "Duraznillo", "Gireth Sharik Alvarado Rubio"),

            ("Espino_Juan_Sebastian_Cuellar_Cardon", "espino", "Espino", "Juan Sebastian Cuellar Cardon"),

            ("Mangle_Juan_Sebastian_Riaño_Fernandez", "mangle", "Mangle", "Juan Sebastian Riaño Fernandez"),
            ("Mangle_Lina_Vanesa_Rico_Laverde", "mangle", "Mangle", "Lina Vanesa Rico Laverde"),

            ("Manzano_Juan_Esteban_Quintana_Rodríguez", "manzano", "Manzano", "Juan Esteban Quintana Rodríguez"),
            ("Manzano_Julian_David_Almonacid_Vanegas", "manzano", "Manzano", "Julian David Almonacid Vanegas"),
            ("Manzano_Mateo_Andres_Guzmán_Reyes", "manzano", "Manzano", "Mateo Andres Guzmán Reyes"),
            ("Manzano_Miguel_Angel_Cartagena_Herrera", "manzano", "Manzano", "Miguel Angel Cartagena Herrera"),
            ("Manzano_Paula_Alejandra_Rincón_Otalvaro", "manzano", "Manzano", "Paula Alejandra Rincón Otalvaro"),

            ("Nogal_Diego_Martínez_Rodríguez", "nogal", "Nogal", "Diego Martínez Rodríguez"),
            ("Pasto_William_Cubillos", "pasto", "Pasto", "William Cubillos"),

            ("Pino_romeron_Daniel_Felipe_Orjuela_Rodríguez", "pino_romeron", "Pino romerón", "Daniel Felipe Orjuela Rodríguez"),
            ("Pino_romeron_Juan_Esteban_Acosta_Peña", "pino_romeron", "Pino romerón", "Juan Esteban Acosta Peña"),
            ("Pino_romerón_Karen_Tatiana_Sandoval_Malagón", "pino_romeron", "Pino romerón", "Karen Tatiana Sandoval Malagón"),
            ("Pino_romerón_Santiago_Correa_Fandiño", "pino_romeron", "Pino romerón", "Santiago Correa Fandiño"),

            ("Roble_Javier_Santiago_Bustos_Laverde", "roble", "Roble", "Javier Santiago Bustos Laverde"),
            ("Roble_Maicol_Stiven_Torres_Rivas", "roble", "Roble", "Maicol Stiven Torres Rivas"),

            ("Sietecueros_Danna_Lucia_Plazas_Lara", "sietecueros", "Sietecueros", "Danna Lucia Plazas Lara"),
            ("Sietecueros_Erick_Santiago_Rodríguez_Paez", "sietecueros", "Sietecueros", "Erick Santiago Rodríguez Paez"),
            ("Sietecueros_Fabian_Andres_Cetina_Rabon", "sietecueros", "Sietecueros", "Fabian Andres Cetina Rabon"),
            ("Sietecueros_Juan_Sebastian_Rocha_Ballen", "sietecueros", "Sietecueros", "Juan Sebastian Rocha Ballen"),
            ("Sietecueros_Karen_Daniela_Bustos_Valero", "sietecueros", "Sietecueros", "Karen Daniela Bustos Valero"),
            ("Sietecueros_Karoll_Alexandra_Duran_Vásquez", "sietecueros", "Sietecueros", "Karoll Alexandra Duran Vásquez"),
            ("Sietecueros_Laura_Daniela_Ibañez_Rodríguez", "sietecueros", "Sietecueros", "Laura Daniela Ibañez Rodríguez"),
            ("Sietecueros_Liseth_Tatiana_Castro_Rodríguez", "sietecueros", "Sietecueros", "Liseth Tatiana Castro Rodríguez"),
            ("Sietecueros_Lyander_Anthony_Hernández_Acosta", "sietecueros", "Sietecueros", "Lyander Anthony Hernández Acosta"),
            ("Sietecueros_Maria_Jose_Castaño_Celis", "sietecueros", "Sietecueros", "Maria Jose Castaño Celis"),
            ("Sietecueros_Mateo_Sánchez_Ramos", "sietecueros", "Sietecueros", "Mateo Sánchez Ramos"),
            ("Sietecueros_Matilde_Bermúdez_Baquero", "sietecueros", "Sietecueros", "Matilde Bermúdez Baquero"),
            ("Sietecueros_Sergio_Danilo_Palacios_Castillo", "sietecueros", "Sietecueros", "Sergio Danilo Palacios Castillo"),
            ("Sietecueros_Susan_Michelle_Doblado_Torres", "sietecueros", "Sietecueros", "Susan Michelle Doblado Torres")
        };

        for (int i = 0; i < variants.Count; i++)
        {
            variantsProperty.InsertArrayElementAtIndex(i);
            SerializedProperty element = variantsProperty.GetArrayElementAtIndex(i);

            string modelKey = variants[i].modelKey;

            element.FindPropertyRelative("modelKey").stringValue = modelKey;
            element.FindPropertyRelative("baseSpeciesId").stringValue = variants[i].speciesId;
            element.FindPropertyRelative("speciesDisplayName").stringValue = variants[i].speciesName;
            element.FindPropertyRelative("subspeciesId").intValue = i + 1;
            element.FindPropertyRelative("instanceId").stringValue = $"{variants[i].speciesId}_{i + 1:00}";
            element.FindPropertyRelative("studentName").stringValue = variants[i].student;

            element.FindPropertyRelative("fase1ResourcePath").stringValue = $"Modelos_3D/fase1/{modelKey}_fase_1";
            element.FindPropertyRelative("fase2ResourcePath").stringValue = $"Modelos_3D/fase2/{modelKey}_fase_2";
            element.FindPropertyRelative("fase3ResourcePath").stringValue = $"Modelos_3D/fase3/{modelKey}_fase_3";
            element.FindPropertyRelative("fase4ResourcePath").stringValue = $"Modelos_3D/fase4/{modelKey}_fase_4";
        }
    }
}