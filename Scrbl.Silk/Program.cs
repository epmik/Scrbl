using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;
using System.Linq;
using System.Numerics;
using Silk.NET.Maths;

namespace Scrbl
{
    class Program
    {
        private static void Main(string[] args)
        {
            //new Scrbl.Tutorials._001_Hello_Window().Run(args);
            //new Scrbl.Tutorials._002_Hello_Quad().Run(args);
            //new Scrbl.Tutorials._003_Hello_Quad_Non_Indexed().Run(args);
            //new Scrbl.Tutorials._004_Hello_Colored_Quad().Run(args);
            //new Scrbl.Tutorials._005_Hello_Colored_Quad_Non_Interleaved().Run(args);
            //new Scrbl.Tutorials._006_Textured_Quad().Run(args);
            //new Scrbl.Tutorials._008_Transformed_Textured_Quad().Run(args);
            //new Scrbl.Tutorials._009_Transformed_Textured_And_Colored_Quad().Run(args);
            //new Scrbl.Tutorials._010_Dynamic_Vertex_Buffer_Writes().Run(args);
            //new Scrbl.Tutorials._011_FrontFace_And_CullFace().Run(args);
            //new Scrbl.Tutorials._012_Static_Lines().Run(args);
            //new Scrbl.Tutorials._013_Dynamic_Lines().Run(args);
            //new Scrbl.Tutorials._014_Dynamic_Lines_Orphaning_Buffer().Run(args);
            //new Scrbl.Tutorials._016_Vao_Vbo_DrawArrays_Tryout().Run(args);
            //new Scrbl.Tutorials._017_Frame_Buffer().Run(args);
            //new Scrbl.Tutorials._017_Frame_Buffer_Struct().Run(args);
            new Scrbl.Tutorials._017_Frame_Buffer_Struct_Multi_Samples().Run(args);

            //Console.Write("Press a key to exit...");
            //Console.ReadKey(false);
        }
    }
}
