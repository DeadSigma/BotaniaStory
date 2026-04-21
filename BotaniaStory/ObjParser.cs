using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Vintagestory.API.Client;

namespace BotaniaStory
{
    public static class ObjParser
    {
        // Вспомогательный класс для хранения данных конкретной детали
        private class PartData
        {
            public List<float> finalXyz = new List<float>();
            public List<float> finalUv = new List<float>();
            public List<int> indices = new List<int>();
            public Dictionary<string, int> vertexCache = new Dictionary<string, int>();
            public int currentIndex = 0;
        }

        public static Dictionary<string, MeshData> Parse(string objText, ICoreClientAPI capi)
        {
            Dictionary<string, MeshData> resultMeshes = new Dictionary<string, MeshData>();

            if (string.IsNullOrWhiteSpace(objText)) return resultMeshes;

            // Глобальные списки вершин (они в OBJ общие для всех деталей)
            List<float[]> vertices = new List<float[]>();
            List<float[]> uvs = new List<float[]>();

            Dictionary<string, PartData> parts = new Dictionary<string, PartData>();
            string currentPartName = "Default"; // Название детали по умолчанию
            parts[currentPartName] = new PartData();

            using (StringReader reader = new StringReader(objText))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                    string[] partsStr = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (partsStr.Length == 0) continue;

                    string type = partsStr[0].ToLowerInvariant();

                    // Если встретили 'o' (object) или 'g' (group) - меняем текущую деталь
                    if (type == "o" || type == "g")
                    {
                        if (partsStr.Length > 1)
                        {
                            currentPartName = partsStr[1];
                            if (!parts.ContainsKey(currentPartName))
                            {
                                parts[currentPartName] = new PartData();
                            }
                        }
                        continue;
                    }

                    if (type == "v")
                    {
                        vertices.Add(new float[] { ParseFloat(partsStr[1]), ParseFloat(partsStr[2]), ParseFloat(partsStr[3]) });
                    }
                    else if (type == "vt")
                    {
                        uvs.Add(new float[] { ParseFloat(partsStr[1]), 1.0f - ParseFloat(partsStr[2]) });
                    }
                    else if (type == "f")
                    {
                        PartData currentPart = parts[currentPartName];

                        for (int i = 1; i <= 3; i++)
                        {
                            if (i >= partsStr.Length) break;

                            string facePart = partsStr[i];
                            if (!currentPart.vertexCache.TryGetValue(facePart, out int index))
                            {
                                string[] indicesStr = facePart.Split('/');

                                int vIdx = int.Parse(indicesStr[0]) - 1;
                                float[] v = vertices[vIdx];
                                currentPart.finalXyz.Add(v[0]);
                                currentPart.finalXyz.Add(v[1]);
                                currentPart.finalXyz.Add(v[2]);

                                if (indicesStr.Length > 1 && !string.IsNullOrEmpty(indicesStr[1]))
                                {
                                    int vtIdx = int.Parse(indicesStr[1]) - 1;
                                    float[] vt = uvs[vtIdx];
                                    currentPart.finalUv.Add(vt[0]);
                                    currentPart.finalUv.Add(vt[1]);
                                }
                                else
                                {
                                    currentPart.finalUv.Add(0f);
                                    currentPart.finalUv.Add(0f);
                                }

                                index = currentPart.currentIndex++;
                                currentPart.vertexCache[facePart] = index;
                            }
                            currentPart.indices.Add(index);
                        }
                    }
                }
            }

            // Превращаем собранные данные в MeshData для каждой детали
            foreach (var kvp in parts)
            {
                string name = kvp.Key;
                PartData data = kvp.Value;

                if (data.indices.Count == 0) continue; // Игнорируем пустые группы

                int vertexCount = data.finalXyz.Count / 3;
                MeshData mesh = new MeshData(vertexCount, data.indices.Count);

                mesh.SetXyz(data.finalXyz.ToArray());
                mesh.SetUv(data.finalUv.ToArray());
                mesh.SetIndices(data.indices.ToArray());

                byte[] rgba = new byte[vertexCount * 4];
                int[] flags = new int[vertexCount];

                for (int i = 0; i < vertexCount; i++)
                {
                    rgba[i * 4 + 0] = 255;
                    rgba[i * 4 + 1] = 255;
                    rgba[i * 4 + 2] = 255;
                    rgba[i * 4 + 3] = 255;
                    flags[i] = 0;
                }

                mesh.Rgba = rgba;
                mesh.Flags = flags;
                mesh.VerticesCount = vertexCount;
                mesh.IndicesCount = data.indices.Count;

                resultMeshes[name] = mesh;
                capi.Logger.Notification($"[BotaniaStory] Загружена часть модели: '{name}' (Вершин: {vertexCount})");
            }

            return resultMeshes;
        }

        private static float ParseFloat(string s)
        {
            s = s.Replace(",", ".");
            return float.Parse(s, CultureInfo.InvariantCulture);
        }
    }
}