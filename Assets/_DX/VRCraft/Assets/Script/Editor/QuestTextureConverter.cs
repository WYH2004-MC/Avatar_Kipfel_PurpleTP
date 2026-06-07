using UnityEngine;
using UnityEditor;
using System.IO;


namespace DX.VRCraft.QuestTextureConverter
{
    public class QuestTextureConverter : EditorWindow
    {
        private Texture2D texture1;

        private string logoPath = "Assets/_DX/VRCraft/Assets/Script/Dependencies/VRCraft Quest Texture Converter.png";
        private string materialPath = "Assets/_DX/VRCraft/Assets/Script/Dependencies/QuestTextureCombiner.mat";
        private string shaderPath = "Assets/_DX/VRCraft/Assets/Script/Dependencies/QuestTextureCombiner.shader";
        private Texture2D logo; // Variável para armazenar a logo
        
        [MenuItem("DX/VRCraft/Quest Texture Converter")]
        public static void ShowWindow()
        {
            QuestTextureConverter window = GetWindow<QuestTextureConverter>(true, "Quest Texture Converter", true);
            
       
            window.ShowUtility(); // Exibe a janela como não encaixável
            window.minSize = new Vector2(500, 209); 
            
            window.maxSize = new Vector2(500, 209); 
            window.position = new Rect(300, 300, 300, 400); 
            window.LoadLogo();

            
        }


        private void LoadLogo()
        {
            // Carrega a logo a partir do caminho do recurso
            logo = AssetDatabase.LoadAssetAtPath<Texture2D>(logoPath);
        }

        void OnGUI()
        {
            
            float sizeW =  EditorGUIUtility.currentViewWidth-5; 
            if (logo != null)
            {
                float imageRatio = (float)logo.height / (float)logo.width;

                GUILayout.BeginHorizontal(); // Inicia o layout horizontal
                GUILayout.FlexibleSpace(); // Adiciona espaço flexível antes da imagem    
                float size =  Mathf.Min(sizeW,500);        
                GUILayout.Label(logo, GUILayout.Width(size), GUILayout.Height(imageRatio*size)); // Exibe a imagem com o tamanho ajustado
                GUILayout.FlexibleSpace(); // Adiciona espaço flexível depois da imagem
                GUILayout.EndHorizontal(); // Finaliza o layout horizontal

            }
            
            GUILayout.Label("Enter the Texture you want to Convert", EditorStyles.boldLabel);

            texture1 = (Texture2D)EditorGUILayout.ObjectField("Texture", texture1, typeof(Texture2D), false);

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) {
                fontSize = 18 // Altere para o tamanho desejado
            };
            // Botão de execução
            if (GUILayout.Button("Combine and Save",buttonStyle, GUILayout.Width(sizeW), GUILayout.Height(30)))
            {
                if (texture1 == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please insert a texture.", "OK");
                }
                else
                {
                    CombineAndSaveTexture(texture1, materialPath);
                }
            }
        }

        private void CombineAndSaveTexture(Texture2D originalTexture, string materialPath)
        {
            // Carrega o material do caminho
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (material == null)
            {
                Debug.LogError("Error: The Material required for conversion does not exist\nTry to reimport VRCraft and try again");
                return;
            }

            if (!File.Exists(shaderPath))
            {
                Debug.LogError("Error: The Shader required for conversion does not exist\nTry to reimport VRCraft and try again");
                return;
            }

            
            string texturePath = AssetDatabase.GetAssetPath(originalTexture);
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            ApplyTextureSettings(importer);

            // Cria uma RenderTexture para armazenar a textura combinada
            RenderTexture combinedTexture = new RenderTexture(originalTexture.width, originalTexture.height, 0);
            RenderTexture.active = combinedTexture;

            // Usa o material para desenhar a textura combinada
            Graphics.Blit(originalTexture, combinedTexture, material);
            RenderTexture.active = null;

            // Converte a RenderTexture para Texture2D
            Texture2D resultTexture = new Texture2D(originalTexture.width, originalTexture.height);
            RenderTexture.active = combinedTexture;
            resultTexture.ReadPixels(new Rect(0, 0, combinedTexture.width, combinedTexture.height), 0, 0);
            resultTexture.Apply();
            // Define configurações da textura gerada
            RenderTexture.active = null;


            // Define o caminho para salvar a nova textura
            string path = AssetDatabase.GetAssetPath(originalTexture);
            string newPath = GenerateUniqueFilePath(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));

            // Salva a nova textura no mesmo local
            File.WriteAllBytes(newPath, resultTexture.EncodeToPNG());
            AssetDatabase.Refresh();

            
            importer = AssetImporter.GetAtPath(newPath) as TextureImporter;
            ApplyTextureSettings(importer);

            

            EditorUtility.DisplayDialog("Success", "Combined texture saved to:\n" + newPath, "OK");
        }
        private void ApplyTextureSettings(TextureImporter importer)
        {

            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.isReadable = true; // Habilita a leitura
                importer.filterMode = FilterMode.Point; // Define o filter mode para Point
                importer.textureCompression = TextureImporterCompression.Uncompressed; // Sem compressão
                importer.mipmapEnabled = true; // Habilita mipmaps
                importer.streamingMipmaps = true; // Ativa Mip Streaming
                importer.alphaIsTransparency = true; // Define a transparência
                importer.SaveAndReimport();
            }
        }


        private string GenerateUniqueFilePath(string directory, string baseFileName)
        {
            string newPath = Path.Combine(directory, baseFileName + "_quest.png");
            int counter = 2;

            // Verifica se o arquivo existe, e incrementa o número até encontrar um nome único
            while (File.Exists(newPath))
            {
                newPath = Path.Combine(directory, baseFileName + $"_quest({counter}).png");
                counter++;
            }

            return newPath;
        }
    }
}
