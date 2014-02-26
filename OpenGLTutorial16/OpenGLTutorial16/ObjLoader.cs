﻿using System;
using System.IO;
using System.Collections.Generic;

using OpenGL;

namespace OpenGLTutorial16
{
    public class ObjLoader : IDisposable
    {
        private List<ObjObject> objects = new List<ObjObject>();
        private Dictionary<string, ObjMaterial> materials = new Dictionary<string, ObjMaterial>();

        public ShaderProgram defaultProgram;

        public ObjLoader(string filename, ShaderProgram program)
        {
            this.defaultProgram = program;

            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            ObjMaterial defaultMaterial = new ObjMaterial(program);

            using (StreamReader stream = new StreamReader(filename))
            {
                List<string> lines = new List<string>();
                int vertexOffset = 1, vertexCount = 0;
                int uvOffset = 1, uvCount = 0;

                // read the entire file
                while (!stream.EndOfStream)
                {
                    string line = stream.ReadLine();
                    if (line.Trim().Length == 0) continue;

                    if ((line[0] == 'o' || line[0] == 'g') && lines.Count != 0)
                    {
                        ObjObject newObject = new ObjObject(lines, materials, vertexOffset, uvOffset);
                        objects.Add(newObject);

                        if (newObject.Material == null) newObject.Material = defaultMaterial;

                        lines.Clear();
                        vertexOffset += vertexCount;
                        uvOffset += uvCount;
                        vertexCount = 0;
                        uvCount = 0;
                    }
                    if (line[0] != '#') lines.Add(line);
                    if (line[0] == 'v')
                    {
                        if (line[1] == ' ') vertexCount++;
                        else uvCount++;
                    }

                    // check if a material file is being used
                    if (line[0] == 'm' && line[1] == 't') LoadMaterials(CreateFixedPath(filename, line.Split(' ')[1]));
                }
            }

            watch.Stop();
            Console.WriteLine("Took {0}ms", watch.ElapsedMilliseconds);
        }

        private void LoadMaterials(string filename)
        {
            using (StreamReader stream = new StreamReader(filename))
            {
                List<string> lines = new List<string>();

                while (!stream.EndOfStream)
                {
                    string line = stream.ReadLine();
                    if (line.Trim().Length == 0) continue;

                    if (line[0] == 'n' && lines.Count != 0)
                    {
                        // if this is a new material ('newmtl name') then load it
                        ObjMaterial material = new ObjMaterial(lines, defaultProgram);
                        if (!materials.ContainsKey(material.Name)) materials.Add(material.Name, material);
                        lines.Clear();
                    }
                    else if (line[0] == 'm')    
                    {
                        // try to fix up filenames of texture maps
                        string[] split = line.Split(' ');
                        lines.Add(string.Format("{0} {1}", split[0], CreateFixedPath(filename, split[1])));
                    }

                    if (line[0] != '#') lines.Add(line);    // ignore comments
                }
            }
        }

        private string CreateFixedPath(string objectPath, string filename)
        {
            if (File.Exists(filename)) return filename;

            DirectoryInfo directory = new FileInfo(objectPath).Directory;

            filename = filename.Replace('\\', '/');
            if (filename.Contains("/")) filename = filename.Substring(filename.LastIndexOf('/') + 1);
            filename = directory.FullName + "\\" + filename;

            return filename;
        }

        public void Draw()
        {
            List<ObjObject> transparentObjects = new List<ObjObject>();

            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i].Material.Transparency != 1f) transparentObjects.Add(objects[i]);
                else objects[i].Draw();
            }

            for (int i = 0; i < transparentObjects.Count; i++)
            {
                transparentObjects[i].Draw();
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < objects.Count; i++) objects[i].Dispose();
        }
    }

    public class ObjMaterial : IDisposable
    {
        public string Name { get; private set; }
        public Vector3 Ambient { get; private set; }
        public Vector3 Diffuse { get; private set; }
        public Vector3 Specular { get; private set; }
        public float SpecularCoefficient { get; private set; }
        public float Transparency { get; private set; }
        public IlluminationMode Illumination { get; private set; }

        public Texture DiffuseMap { get; private set; }
        public ShaderProgram Program { get; private set; }

        public enum IlluminationMode
        {
            ColorOnAmbientOff = 0,
            ColorOnAmbientOn = 1,
            HighlightOn = 2,
            ReflectionOnRaytraceOn = 3,
            TransparencyGlassOnReflectionRayTraceOn = 4,
            ReflectionFresnelOnRayTranceOn = 5,
            TransparencyRefractionOnReflectionFresnelOffRayTraceOn = 6,
            TransparencyRefractionOnReflectionFresnelOnRayTranceOn = 7,
            ReflectionOnRayTraceOff = 8,
            TransparencyGlassOnReflectionRayTraceOff = 9,
            CastsShadowsOntoInvisibleSurfaces = 10
        }

        public ObjMaterial(ShaderProgram program)
        {
            this.Name = "opengl-default-project";
            this.Transparency = 1f;
            this.Ambient = Vector3.UnitScale;
            this.Diffuse = Vector3.UnitScale;
            this.Program = program;
        }

        public ObjMaterial(List<string> lines, ShaderProgram program)
        {
            if (!lines[0].StartsWith("newmtl")) return;

            this.Name = lines[0].Substring(7);
            this.Transparency = 1f;

            for (int i = 1; i < lines.Count; i++)
            {
                string[] split = lines[i].Split(' ');

                switch (split[0])
                {
                    case "Ns": this.SpecularCoefficient = float.Parse(split[1]);
                        break;
                    case "Ka": this.Ambient = new Vector3(float.Parse(split[1]), float.Parse(split[2]), float.Parse(split[3]));
                        break;
                    case "Kd": this.Diffuse = new Vector3(float.Parse(split[1]), float.Parse(split[2]), float.Parse(split[3]));
                        break;
                    case "Ks": this.Specular = new Vector3(float.Parse(split[1]), float.Parse(split[2]), float.Parse(split[3]));
                        break;
                    case "d": this.Transparency = float.Parse(split[1]);
                        break;
                    case "illum": this.Illumination = (IlluminationMode)int.Parse(split[1]);
                        break;
                    case "map_Kd": if (File.Exists(split[1])) this.DiffuseMap = new Texture(split[1]);
                        else if (File.Exists(split[1].Substring(split[1].LastIndexOf('\\')))) new Texture(split[1].Substring(split[1].LastIndexOf('\\')));
                        else throw new FileNotFoundException("Could not find the file " + split[1] + " to be used as a diffuse map.");
                        break;
                }
            }

            this.Program = program;
        }

        public void Use()
        {
            if (DiffuseMap != null)
            {
                Gl.ActiveTexture(TextureUnit.Texture0);
                Gl.BindTexture(this.DiffuseMap);
                this.Program["useTexture"].SetValue(true);
            }
            else this.Program["useTexture"].SetValue(false);

            this.Program.Use();

            this.Program["diffuse"].SetValue(this.Diffuse);
            this.Program["texture"].SetValue(0);
            this.Program["transparency"].SetValue(this.Transparency);
        }

        public void Dispose()
        {
            if (DiffuseMap != null) DiffuseMap.Dispose();
            if (Program != null)
            {
                Program.DisposeChildren = true;
                Program.Dispose();
            }
        }
    }

    public class ObjObject : IDisposable
    {
        private VBO<Vector3> vertices;
        private VBO<Vector3> normals;
        private VBO<Vector2> uvs;
        private VBO<int> triangles;

        public string Name { get; private set; }

        public ObjMaterial Material { get; set; }

        public ObjObject(List<string> lines, Dictionary<string, ObjMaterial> materials, int vertexOffset, int uvOffset)
        {
            // we need at least 1 line to be a valid file
            if (lines.Count == 0) return;

            // the first line should contain 'o'
            if (lines[0][0] != 'o' && lines[0][0] != 'g') return;
            this.Name = lines[0].Substring(2);

            List<Vector3> vertexList = new List<Vector3>();
            List<Vector2> uvList = new List<Vector2>();
            List<int> triangleList = new List<int>();
            List<Vector2> unpackedUvs = new List<Vector2>();
            List<int> normalsList = new List<int>();

            // now we read the lines
            for (int i = 1; i < lines.Count; i++)
            {
                string[] split = lines[i].Split(' ');

                switch (split[0])
                {
                    case "v":
                        vertexList.Add(new Vector3(double.Parse(split[1]), double.Parse(split[2]), double.Parse(split[3])) * 0.025f);
                        break;
                    case "vt":
                        uvList.Add(new Vector2(double.Parse(split[1]), double.Parse(split[2])));
                        break;
                    case "f":
                        string[] indices = new string[] { split[1], split[2], split[3] };

                        if (split[1].Contains("/"))
                        {
                            indices[0] = split[1].Substring(0, split[1].IndexOf("/"));
                            indices[1] = split[2].Substring(0, split[2].IndexOf("/"));
                            indices[2] = split[3].Substring(0, split[3].IndexOf("/"));

                            string[] uvs = new string[3];
                            uvs[0] = split[1].Substring(split[1].IndexOf("/") + 1);
                            uvs[1] = split[2].Substring(split[2].IndexOf("/") + 1);
                            uvs[2] = split[3].Substring(split[3].IndexOf("/") + 1);

                            int[] triangle = new int[] { int.Parse(indices[0]) - vertexOffset, int.Parse(indices[1]) - vertexOffset, int.Parse(indices[2]) - vertexOffset };

                            if (unpackedUvs.Count == 0) for (int j = 0; j < vertexList.Count; j++) unpackedUvs.Add(Vector2.Zero);
                            normalsList.Add(triangle[0]);
                            normalsList.Add(triangle[1]);
                            normalsList.Add(triangle[2]);

                            if (unpackedUvs[triangle[0]] == Vector2.Zero) unpackedUvs[triangle[0]] = uvList[int.Parse(uvs[0]) - uvOffset];
                            else
                            {
                                unpackedUvs.Add(uvList[int.Parse(uvs[0]) - uvOffset]);
                                vertexList.Add(vertexList[triangle[0]]);
                                triangle[0] = unpackedUvs.Count - 1;
                            }

                            if (unpackedUvs[triangle[1]] == Vector2.Zero) unpackedUvs[triangle[1]] = uvList[int.Parse(uvs[1]) - uvOffset];
                            else
                            {
                                unpackedUvs.Add(uvList[int.Parse(uvs[1]) - uvOffset]);
                                vertexList.Add(vertexList[triangle[1]]);
                                triangle[1] = unpackedUvs.Count - 1;
                            }

                            if (unpackedUvs[triangle[2]] == Vector2.Zero) unpackedUvs[triangle[2]] = uvList[int.Parse(uvs[2]) - uvOffset];
                            else
                            {
                                unpackedUvs.Add(uvList[int.Parse(uvs[2]) - uvOffset]);
                                vertexList.Add(vertexList[triangle[2]]);
                                triangle[2] = unpackedUvs.Count - 1;
                            }

                            triangleList.Add(triangle[0]);
                            triangleList.Add(triangle[1]);
                            triangleList.Add(triangle[2]);
                        }
                        else
                        {
                            triangleList.Add(int.Parse(indices[0]) - vertexOffset);
                            triangleList.Add(int.Parse(indices[1]) - vertexOffset);
                            triangleList.Add(int.Parse(indices[2]) - vertexOffset);
                        }
                        break;
                    case "usemtl":
                        if (materials.ContainsKey(split[1])) Material = materials[split[1]];
                        break;
                }
            }

            // calculate the normals (if they didn't exist)
            Vector3[] vertexData = vertexList.ToArray();
            int[] elementData = triangleList.ToArray();
            Vector3[] normalData = OpenGL.Geometry.CalculateNormals(vertexData, elementData);

            // now convert the lists over to vertex buffer objects to be rendered by OpenGL
            this.vertices = new VBO<Vector3>(vertexData);
            this.normals = new VBO<Vector3>(normalData);
            if (unpackedUvs.Count != 0) this.uvs = new VBO<Vector2>(unpackedUvs.ToArray());
            this.triangles = new VBO<int>(elementData, BufferTarget.ElementArrayBuffer);
        }

        public void Draw()
        {
            if (vertices == null || triangles == null) return;
            //if (Material == null) return;

            Gl.Disable(EnableCap.CullFace);
            if (Material != null) Material.Use();

            Gl.BindBufferToShaderAttribute(vertices, Material.Program, "vertexPosition");
            Gl.BindBufferToShaderAttribute(normals, Material.Program, "vertexNormal");
            if (uvs != null) Gl.BindBufferToShaderAttribute(uvs, Material.Program, "vertexUV");
            Gl.BindBuffer(triangles);

            Gl.DrawElements(BeginMode.Triangles, triangles.Count, DrawElementsType.UnsignedInt, IntPtr.Zero);
        }

        public void Dispose()
        {
            if (vertices != null) vertices.Dispose();
            if (normals != null) normals.Dispose();
            if (triangles != null) triangles.Dispose();
        }
    }
}
