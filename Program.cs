using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCFCompiler
{
    class Program
    {
        static void Main(string[] args)
        {
            IntoAST.CompileFile("C:\\Users\\caleb\\source\\repos\\MCFCompiler\\bin\\Debug\\", "C:\\Users\\caleb\\source\\repos\\MCFCompiler\\input.txt");

            Console.ReadKey();
        }
    }
}
