using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Vintagestory.API.Client;

namespace BotaniaStory
{
    public static class ObjParser
    {
        public static MeshData Parse(string objText, ICoreClientAPI capi)
        {
            List<float[]> vertices = new List<float[]>();
            List<float[]> uvs = new List<float[]>();

            List<float> finalXyz = new List<float>();
            List<float> finalUv = new List<float>();
            List<int> indices = new List<int>();

            Dictionary<string, int> vertexCache = new Dictionary<string, int>();
            int currentIndex = 0;

            int vCount = 0;
            int vtCount = 0;
            int fCount = 0;
            int skippedCount = 0;
            bool firstFaceLogged = false;

            if (string.IsNullOrWhiteSpace(objText))
            {
                return new MeshData(1, 1);
            }

            using (StringReader reader = new StringReader(objText))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();

                    if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                    string[] parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0) continue;

                    string type = parts[0].ToLowerInvariant();

                    try
                    {
                        if (type == "v")
                        {
                            vCount++;
                            vertices.Add(new float[] {
                                ParseFloat(parts[1]),
                                ParseFloat(parts[2]),
                                ParseFloat(parts[3])
                            });
                        }
                        else if (type == "vt")
                        {
                            vtCount++;
                            uvs.Add(new float[] {
                                ParseFloat(parts[1]),
                                1.0f - ParseFloat(parts[2])
                            });
                        }
                        else if (type == "f")
                        {
                            fCount++;

                            // Логируем самый первый полигон, чтобы понять его структуру
                            if (!firstFaceLogged)
                            {
                                capi.Logger.Notification($"[BotaniaStory] Первый полигон выглядит так: '{line}'");
                                firstFaceLogged = true;
                            }

                            for (int i = 1; i <= 3; i++)
                            {
                                if (i >= parts.Length) break;

                                string facePart = parts[i];
                                if (!vertexCache.TryGetValue(facePart, out int index))
                                {
                                    string[] indicesStr = facePart.Split('/');

                                    int vIdx = int.Parse(indicesStr[0]) - 1;
                                    float[] v = vertices[vIdx];
                                    finalXyz.Add(v[0]);
                                    finalXyz.Add(v[1]);
                                    finalXyz.Add(v[2]);

                                    if (indicesStr.Length > 1 && !string.IsNullOrEmpty(indicesStr[1]))
                                    {
                                        int vtIdx = int.Parse(indicesStr[1]) - 1;
                                        float[] vt = uvs[vtIdx];
                                        finalUv.Add(vt[0]);
                                        finalUv.Add(vt[1]);
                                    }
                                    else
                                    {
                                        finalUv.Add(0f);
                                        finalUv.Add(0f);
                                    }

                                    index = currentIndex++;
                                    vertexCache[facePart] = index;
                                }
                                indices.Add(index);
                            }
                        }
                        else
                        {
                            skippedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        capi.Logger.Error($"[BotaniaStory] Ошибка на строке '{line}': {ex.Message}");
                    }
                }
            }

            capi.Logger.Notification($"[BotaniaStory] ИТОГИ ПАРСИНГА: Вершин(v)={vCount}, Текстур(vt)={vtCount}, Полигонов(f)={fCount}, Пропущено={skippedCount}");
            capi.Logger.Notification($"[BotaniaStory] Итоговый размер массивов: Xyz={finalXyz.Count}, Индексы={indices.Count}");

            // 1. Инициализируем MeshData и говорим, что у нас БУДУТ цвета (rgba) и флаги (flags)
            // Сигнатура: (vertices, indices, normals, rgba, uv, flags)
            int vertexCount = finalXyz.Count / 3;
            MeshData mesh = new MeshData(vertexCount, indices.Count, false, true, true, true);

            mesh.SetXyz(finalXyz.ToArray());
            mesh.SetUv(finalUv.ToArray());
            mesh.SetIndices(indices.ToArray());

            // 2. Создаем обязательные массивы цвета и флагов для шейдера
            byte[] rgba = new byte[vertexCount * 4];
            int[] flags = new int[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                // Заполняем цвет белым (чтобы текстура отображалась как есть)
                rgba[i * 4 + 0] = 255; // R
                rgba[i * 4 + 1] = 255; // G
                rgba[i * 4 + 2] = 255; // B
                rgba[i * 4 + 3] = 255; // A

                // Флаги (0 = нет анимации ветра, нет свечения)
                flags[i] = 0;
            }

            // Присваиваем массивы нашему мешу
            mesh.Rgba = rgba;
            mesh.Flags = flags;

            // 3. Та самая магия количества
            mesh.VerticesCount = vertexCount;
            mesh.IndicesCount = indices.Count;

            return mesh;
        }

        private static float ParseFloat(string s)
        {
            s = s.Replace(",", ".");
            return float.Parse(s, CultureInfo.InvariantCulture);
        }
    }
}