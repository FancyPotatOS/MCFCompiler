using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Configuration;
using System.CodeDom.Compiler;
using System.Xml;
using System.Globalization;

namespace MCFCompiler
{
    static class Reserved
    {
        public static string[] reservedKeywords = { "desig", "var", "func", "lit", "call", "context", "run", "endrun", ";", "rule" };
        
        // List of names that exist
        public static List<VariableDeclaration> vEx = new List<VariableDeclaration>();
        public static List<DesigDeclaration> dEx = new List<DesigDeclaration>();
        public static List<FunctionDeclaration> fEx = new List<FunctionDeclaration>();

        // Whether s exists as name or check if keyword if des
        public static bool Exists(string s, bool dec)
        {
            // If string is a reserved keyword
            if (dec && reservedKeywords.Contains(s))
            {
                throw new Exception("Name " + s + " is a reserved keyword!");
            }

            return vEx.Exists(v => v.varName == s) || dEx.Exists(d => d.desigName == s) || fEx.Exists(f => false);
        }

        // String of commands to initialize all variables and desigs
        public static string InitializeDeclarationString()
        {
            // Add variable scoreboard
            string coll = "scoreboard objectives add " + IntoAST.nameSpace + "Var dummy\n";
            
            // Set all variables to 0
            foreach (VariableDeclaration vd in vEx)
            {
                coll += "scoreboard players set " + vd.varName + " " + IntoAST.nameSpace + "Var 0\n";
            }

            // Create all desig scoreboards
            foreach (DesigDeclaration dd in dEx)
            {
                coll += "scoreboard objectives add " + dd.desigName + " " + dd.criteria + "\n";
            }

            return coll;
        }
    }
    
    // Can be a child to a function declaration
    interface IChildFunctionDeclaration {
        string GetLine();
    }
    class FunctionDeclaration : IChildMainFile
    {

        // List of things inside function
        List<IChildFunctionDeclaration> todo = new List<IChildFunctionDeclaration>();

        public string funcName;
        public string context = "";

        // Parse into function
        public int Parse(string[] parts, int start)
        {
            // Set compilation time context
            string currContext = context;

            // If doesn't start with keyword func
            if (parts[start] != "func")
            {
                throw new Exception("Func starts with '" + parts[start] + "' not 'func'!");
            }
            // If the name is already taken
            else if (Reserved.Exists(parts[start + 1], true))
            {
                throw new Exception("Name " + parts[start + 1] + " already exists!");
            }
            else if (parts[start + 2] != "{")
            {
                throw new Exception("Expected { not " + parts[start + 2]);
            }
            else
            {
                // Save function name
                funcName = parts[start + 1];
                Reserved.fEx.Add(this);

                // Skip to first expression
                start += 3;

                // Keep identifying expression until end of function declaration
                while (start < parts.Length)
                {
                    if (parts[start] == "}")
                    {
                        return start + 1;
                    }

                    // Identify from function context
                    IntoAST.IdentifyResponse ir = IntoAST.Identify("function", currContext, parts, start);

                    // Move to next statement and add function child to todo
                    start = ir.next;
                    if (ir.IChild is IChildFunctionDeclaration icfd)
                    {
                        todo.Add(icfd);
                    }
                    else
                    {
                        throw new Exception("Function child '" + ir.IChild + "' cannot follow function '" + funcName + "'");
                    }
                }

                // Didn't find end of function
                throw new Exception("No } found for function " + funcName);
            }
        }

        public void CreateFunction(string directory)
        {
            string filepath = directory + funcName + ".mcfunction";
            if (File.Exists(filepath))
            {
                throw new Exception("Function '" + funcName + "'s .mcfunction already exists!");
            }
            else
            {
                string alldo = "";
                // There is a load rule, and the load function is this function
                if (Ruling.hasLoad && IntoAST.maintodo.Exists(x => x is LoadFuncRuling lf && lf.function == funcName))
                {
                    // Begin with the initialization
                    alldo = Reserved.InitializeDeclarationString();
                }

                // Open the file to the function
                StreamWriter sw = File.CreateText(filepath);

                foreach (IChildFunctionDeclaration icdf in todo)
                {
                    alldo += icdf.GetLine() + "\n";
                }

                // Write to file as text
                sw.Write(alldo);

                sw.Close();

                {}
            }
        }
    }

    class VariableDeclaration : IChildMainFile
    {

        // Name of var
        public string varName;

        // Parse the declaration and returns index of parts after declaration
        public int Parse(string[] parts, int start)
        {
            // If doesn't start with keyword
            if (parts[start] != "var")
            {
                throw new Exception("Var starts with '" + parts[start] + "' not 'var'!");
            }
            // If the name is already taken
            else if (Reserved.Exists(parts[start + 1], true))
            {
                throw new Exception("Name " + parts[start + 1] + " already exists!");
            }
            // Declaration doesn't end with ;
            else if (parts[start + 2] != ";")
            {
                throw new Exception("Var declaration doesn't end in ';'!");
            }
            else
            {
                // Save var name
                varName = parts[start + 1];
                Reserved.vEx.Add(this);

                // Return index after var
                return start + 3;
            }
        }
    }
    
    // Desig declaration
    class DesigDeclaration : IChildMainFile
    {
        // List of desigs that exist
        public static List<string> existing = new List<string>();

        // Name of desig
        public string criteria;
        public string desigName;

        // Parse the declaration and returns index of parts after declaration
        public int Parse(string[] parts, int start)
        {
            // If doesn't start with keyword
            if (parts[start] != "desig")
            {
                throw new Exception("Desig starts with '" + parts[start] + "' not 'desig'!");
            }
            // If the name is already taken
            else if (Reserved.Exists(parts[start + 1], true))
            {
                throw new Exception("Name " + parts[start + 1] + " already exists!");
            }
            // Declaration doesn't end with ;
            else if (parts[start + 3] != ";")
            {
                throw new Exception("Desig declaration doesn't end in ';'!");
            }
            else
            {
                // Save desig name
                desigName = parts[start + 1];
                criteria = parts[start + 2];
                Reserved.dEx.Add(this);

                // Return index after desig
                return start + 4;
            }
        }
    }

    // Runs raw command
    class RunCommand : IChildFunctionDeclaration
    {
        public string context;
        public string command;

        public int Parse(string[] parts, int start)
        {
            if (parts[start] != "run")
            {
                throw new Exception("Run starts with '" + parts[start] + " not 'run'!");
            }
            else
            {
                // Reset command
                command = "";
                start++;

                // Gather command until 'endrun'
                while (parts[start] != "endrun")
                {
                    // Add to command
                    command += parts[start];

                    // Add space between parts if there is next statement
                    if (parts[start + 1] != "endrun")
                        command += " ";
                    
                    start++;
                }

                // Skip 'endrun'
                return start + 1;
            }
        }

        public string GetLine()
        {
            return context + command;
        }
    }

    // Change in context, with same criteria as function
    class ContextChange : IChildFunctionDeclaration
    {
        // Context taken from
        public string context;
        // Context things inside run in
        public string change;

        List<IChildFunctionDeclaration> todo = new List<IChildFunctionDeclaration>();

        // Parse into function
        public int Parse(string[] parts, int start)
        {
            // If doesn't start with keyword func
            if (parts[start] != "context")
            {
                throw new Exception("Context change starts with '" + parts[start] + "' not 'context'!");
            }

            start++;
            // Get the change in context
            change = context + "execute ";
            // While hasn't reached code block start
            while (parts[start] != "{")
            {
                // Add change
                change += parts[start] + " ";

                start++;
            }
            change += "run ";

            // This is impossible
            if (parts[start] != "{")
            {
                throw new Exception("Expected { not " + parts[start]);
            }
            else
            {
                // Skip {
                start += 1;

                // Keep identifying expression until end of function declaration
                while (start < parts.Length)
                {
                    if (parts[start] == "}")
                    {
                        return start + 1;
                    }

                    // Identify from function context
                    IntoAST.IdentifyResponse ir = IntoAST.Identify("function", change, parts, start);
                    // Move to next statement and add function child to todo
                    start = ir.next;
                    if (ir.IChild is IChildFunctionDeclaration icfd)
                    {
                        todo.Add(icfd);
                    }
                    else
                    {
                        throw new Exception("Context child '" + ir.IChild + "' cannot follow context change '" + change + "'");
                    }
                }

                // Didn't find end of function
                throw new Exception("No } found for context change " + change);
            }
        }

        /*

        public void CreateFunction(string directory)
        {
            string filepath = directory + funcName + ".mcfunction";
            if (File.Exists(filepath))
            {
                throw new Exception("Function '" + funcName + "'s .mcfunction already exists!");
            }
            else
            {
                StreamWriter sw = File.CreateText(filepath);

                string alldo = "";
                foreach (IChildFunctionDeclaration icdf in todo)
                {
                    alldo += icdf.GetLine() + "\n";
                }

                // Write to file as text
                sw.Write(alldo);

                sw.Close();

                {}
            }
        }*/

        public string GetLine()
        {
            string alldo = "";

            foreach (IChildFunctionDeclaration icdf in todo)
            {
                alldo += icdf.GetLine() + "\n";
            }

            return alldo;
        }
    }

    // Whether be a child to a variable assignation
    interface IChildVariableAssignation 
    {
        string GetExpVar(string op, string varname);
    }
    class VariableAssignation : IChildFunctionDeclaration
    {
        // List of assigning operators
        public static List<string> assignOps = new string[] { "=", "+=", "-=", "*=", "/=", "%=" }.ToList();
        
        // Context of assignation
        public string context;

        // Declarationn of variable
        public VariableDeclaration var;

        public string op;

        // Variable expression
        public IChildVariableAssignation exp;

        // Parses the variable assignation
        public int Parse(string[] parts, int start)
        {
            // Variable is not declared
            if (!Reserved.vEx.Exists(p => p.varName == parts[start]))
            {
                throw new Exception("Variable '" + parts[start] + "' is not declared!");
            }
            // The second part is not proper assigning operator
            else if (!assignOps.Contains(parts[start + 1]))
            {
                throw new Exception("Variable assignation has improper assigning operator\t\tFormat: [varname] [=, +=, -=, *=, /=, or %=] [const or literal integer] ;!");
            }
            // Get expression
            else
            {
                // Save operator
                op = parts[start + 1];

                // Save Variable
                var = Reserved.vEx.Find(v => v.varName == parts[start]);

                // Identify type
                IntoAST.IdentifyResponse ir = IntoAST.Identify("variable expression", context, parts, start + 2);

                // Check type
                if (ir.IChild is IChildVariableAssignation icva)
                {
                    // Move to next
                    start = ir.next;
                    exp = icva;
                }
                else
                {
                    throw new Exception("Invalid variable subexpression '" + ir.IChild + "'");
                }

                // Return index after expression
                return start;
            }
        }

        public string GetLine()
        {
            return context + "scoreboard players " + exp.GetExpVar(op, var.varName);
        }
    }

    // Can be a child to a desig assignation
    interface IChildDesigAssignation 
    {
        string GetExpDesig(string op, string designame);
    }
    class DesigAssignation : IChildFunctionDeclaration
    {

        // Declarationn of variable
        public DesigDeclaration desig;

        public string op;

        // Expression to deconstruct
        public IChildDesigAssignation exp;
        
        // Context of assignation
        public string context;

        // List of assigning operators
        public static List<string> desigOps = new string[] { "=", "+=", "-=", "*=", "/=", "%=" }.ToList();

        // Parses the variable assignation
        public int Parse(string[] parts, int start)
        {
            // Variable is not declared
            if (!Reserved.dEx.Exists(p => p.desigName == parts[start]))
            {
                throw new Exception("Desig '" + parts[start] + "' is not declared!");
            }
            // The second part is not proper assigning operator
            else if (!desigOps.Contains(parts[start + 1]))
            {
                throw new Exception("Desig assignation has improper assigning operator '" + parts[start + 1] + "'!");
            }
            // Get expression
            else
            {
                // Save operator
                op = parts[start + 1];

                // Save desig
                desig = Reserved.dEx.Find(d => d.desigName == parts[start]);

                // Identify type
                IntoAST.IdentifyResponse ir = IntoAST.Identify("desig expression", context, parts, start + 2);

                // Check type
                if (ir.IChild is IChildDesigAssignation icda)
                {
                    // Move to next
                    start = ir.next;
                    exp = icda;
                }
                else
                {
                    throw new Exception("Invalid desig subexpression '" + ir.IChild + "'");
                }

                // Return index after expression
                return start;
            }
        }

        public string GetLine()
        {
            return context + "scoreboard players " + exp.GetExpDesig(op, desig.desigName);
        }
    }

    // Literal can be in a assignation or expression
    class Literal : IChildVariableAssignation, IChildDesigAssignation
    {
        int value = 0;

        // Parses expression and keeps value
        public int Parse(string[] parts, int start)
        {
            if (parts[start] != "lit")
            {
                throw new Exception("Literal starts with " + parts[start] + " not 'lit'!");
            }
            else if (parts[start + 2] != ";")
            {
                throw new Exception("Literal ends with with " + parts[start + 2] + " not ';'!");
            }
            bool parsed = Int32.TryParse(parts[start + 1], out value);
            if (!parsed)
            {
                throw new Exception ("Could not parse '" + parts[start + 1] + "'!");
            }
            return start + 3;
        }

        // set <varname> <namespace>Var <value>
        public string GetExpVar(string op, string varname)
        {
            if (op == "=")
            {
                return "set " + varname + " " + IntoAST.nameSpace + "Var " + value;
            }
            else if (op == "+=")
            {
                return "add " + varname + " " + IntoAST.nameSpace + "Var " + value;
            }
            else if (op == "-=")
            {
                return "remove " + varname + " " + IntoAST.nameSpace + "Var " + value;
            }
            else if (op == "*=" || op == "%=" || op == "/=")
            {
                throw new Exception("Operation '" + op + "' cannot be performed on a literal!");
            }
            else
            {
                throw new Exception("Unrecognized operation " + op + "!");
            }
        }

        public string GetExpDesig(string op, string designame)
        {
            if (op == "=")
            {
                return "set @s " + designame + " " + value;
            }
            else if (op == "+=")
            {
                return "add @s " + designame + " " + value;
            }
            else if (op == "-=")
            {
                return "remove @s " + designame + " " + value;
            }
            else if (op == "*=" || op == "%=" || op == "/=")
            {
                throw new Exception("Operation '" + op + "' cannot be performed on a literal!");
            }
            else
            {
                throw new Exception("Unrecognized operation '" + op + "'!");
            }
        }
    }

    // Variable as a subexpression
    class VarSubExpression: IChildVariableAssignation, IChildDesigAssignation
    {
        VariableDeclaration vd;

        // Parses into variable
        public int Parse(string[] parts, int start)
        {
            // Ends with ;
            if (parts[start + 1] != ";")
            {
                throw new Exception("Subexpression ends with " + parts[start] + " not ';'!");
            }

            VariableDeclaration varDec = Reserved.vEx.Find(v => v.varName == parts[start]);
            // Checks if variable name
            if (varDec == null)
            {
                throw new Exception (parts[start] + " is not a variable name!");
            }
            else 
            {
                // Is a variable name
                vd = varDec;

                // Return position after
                return start + 2;
            }
        }

        // operation <varname> <namespace>Var <op> <othervar> <namespace>Var
        public string GetExpVar(string op, string varname)
        {
            return "operation " + varname + " " + IntoAST.nameSpace + "Var " + op + " " + vd.varName + " " + IntoAST.nameSpace + "Var";
        }

        // operation @s <designame> <op> <othervar> <namespace>Var
        public string GetExpDesig(string op, string designame)
        {
            return "operation @s " + designame + " " + op + " " + vd.varName + " " + IntoAST.nameSpace + "Var";
        }
    }

    // Desig as a subexpression
    class DesigSubExpression: IChildDesigAssignation
    {
        DesigDeclaration dd;

        // Parses into variable
        public int Parse(string[] parts, int start)
        {
            // Ends with ;
            if (parts[start + 1] != ";")
            {
                throw new Exception("Subexpression ends with " + parts[start] + " not ';'!");
            }

            DesigDeclaration desDec = Reserved.dEx.Find(d => d.desigName == parts[start]);
            // Checks if desig name
            if (desDec == null)
            {
                throw new Exception (parts[start] + " is not a desig name!");
            }
            else 
            {
                // Is a desig name
                dd = desDec;

                // Return position after
                return start + 2;
            }
        }

        // operation @s <designame> <op> @s <otherdesigname>
        public string GetExpDesig(string op, string designame)
        {
            return "operation @s " + designame + " " + op + " @s " + dd.desigName;
        }
    }

    // Function call
    class FuncCall : IChildFunctionDeclaration
    {
        FunctionDeclaration fd;
        
        // Context of call
        public string context;

        // Parses into function call
        public int Parse(string[] parts, int start)
        {
            // Start with call keyword
            if (parts[start] != "call")
            {
                throw new Exception("Function call starts with '" + parts[start] + " not 'call'!");
            }
            else if (parts[start + 2] != ";")
            {
                throw new Exception("Function call does not end with ';'!");
            }
            
            // Gets declaration
            FunctionDeclaration tempFD = Reserved.fEx.Find(f => f.funcName == parts[start + 1]);
            if (tempFD == null)
            {
                throw new Exception("Function '" + parts[start + 1] + "' does not exist!");
            }
            else
            {
                fd = tempFD;

                return start + 3;
            }
        }

        public string GetLine()
        {
            return context + "function " + IntoAST.nameSpace + ":" + fd.funcName;
        }
    }

    // Can be a child to the main file
    public interface IChildMainFile { }
    class IntoAST
    {
        public static List<IChildMainFile> maintodo;

        public static CompilationOfFiles cof = new CompilationOfFiles();

        public static string nameSpace;

        public IntoAST(string textfile)
        {
            maintodo = new List<IChildMainFile>();

            /*  Clean string    */
            // Newline to spaces
            string cleaned = textfile.Replace("\n", " ");
            // Tabs to spaces
            cleaned = cleaned.Replace("\t", " ");
            // Split by spaces
            string[] parts = cleaned.Split(new char[] {' '});
            // Remove empty parts (aka "")
            parts = parts.ToList().FindAll(p => p != null && (p != "")).ToArray();

            // Save context
            string context = "mainfile";

            // For every part
            for (int i = 0; i < parts.Length;)
            {
                // Identify next statement
                IdentifyResponse ir = Identify(context, "", parts, i);

                // Go to next statement
                i = ir.next;

                // Add to main todo
                maintodo.Add(ir.IChild as IChildMainFile);
            }
        }

        public class IdentifyResponse
        {
            public string context;
            public Object IChild;
            public int next;

            public IdentifyResponse(Object i, string c, int n)
            {
                IChild = i;
                context = c;
                next = n;
            }
        }
        
        // Identifies type given and parses into type. expContext is context that it will run on
        public static IdentifyResponse Identify(string context, string expContext, string[] parts, int start)
        {
            // If it is a reserved keyword
            if (Reserved.reservedKeywords.Contains(parts[start]))
            {
                /*  Main File   */
                // If declaring desig
                if (context == "mainfile" && parts[start] == "desig")
                {
                    DesigDeclaration dd = new DesigDeclaration();
                    return new IdentifyResponse(dd, expContext, dd.Parse(parts, start));
                }
                // If declaring variable
                else if (context == "mainfile" && parts[start] == "var")
                {
                    VariableDeclaration vd = new VariableDeclaration();
                    return new IdentifyResponse(vd, expContext, vd.Parse(parts, start));
                }
                // If declaring function
                else if (context == "mainfile" && parts[start] == "func")
                {
                    FunctionDeclaration fd = new FunctionDeclaration();
                    return new IdentifyResponse(fd, expContext, fd.Parse(parts, start));
                }
                // If declaring ruling
                else if (context == "mainfile" && parts[start] == "rule")
                {
                    RulingResponse rr = Ruling.IdentifyRuling(parts, start);
                    
                    return new IdentifyResponse(rr.icmf, expContext, rr.start);
                }
                /*  Function    */
                // If it is a function call
                else if (context == "function" && parts[start] == "call")
                {
                    FuncCall fc = new FuncCall();
                    fc.context = expContext;
                    return new IdentifyResponse(fc, expContext, fc.Parse(parts, start));
                }
                // If it is a context change
                else if (context == "function" && parts[start] == "context")
                {
                    ContextChange cc = new ContextChange();
                    cc.context = expContext;
                    return new IdentifyResponse(cc, expContext, cc.Parse(parts, start));
                }
                // If it is a specified command
                else if (context == "function" && parts[start] == "run")
                {
                    RunCommand rc = new RunCommand();
                    rc.context = expContext;
                    return new IdentifyResponse(rc, expContext, rc.Parse(parts, start));
                }
                /*  Variable Assignation    */
                // If it is a literal
                else if (context == "variable expression" && parts[start] == "lit")
                {
                    Literal lit = new Literal();
                    return new IdentifyResponse(lit, expContext, lit.Parse(parts, start));
                }
                /*  Desig Assignation   */
                // If it is a literal
                else if (context == "desig expression" && parts[start] == "lit")
                {
                    Literal lit = new Literal();
                    return new IdentifyResponse(lit, expContext, lit.Parse(parts, start));
                }
            }
            // Else if it is a Var assignation
            else if (context == "function" && Reserved.vEx.Exists(vd => vd.varName == parts[start]))
            {
                VariableAssignation va = new VariableAssignation();
                va.context = expContext;
                return new IdentifyResponse(va, expContext, va.Parse(parts, start));
            }
            // Else if it is a Desig assignation
            else if (context == "function" && Reserved.dEx.Exists(dd => dd.desigName == parts[start]))
            {
                DesigAssignation da = new DesigAssignation();
                da.context = expContext;
                return new IdentifyResponse(da, expContext, da.Parse(parts, start));
            }
            // If it is a variable
            else if (context == "variable expression" && Reserved.vEx.Exists(v => v.varName == parts[start]))
            {
                VarSubExpression vse = new VarSubExpression();
                return new IdentifyResponse(vse, expContext, vse.Parse(parts, start));
            }
            // If it is a variable
            else if (context == "desig expression" && Reserved.vEx.Exists(v => v.varName == parts[start]))
            {
                VarSubExpression vse = new VarSubExpression();
                return new IdentifyResponse(vse, expContext, vse.Parse(parts, start));
            }
            // If it is a desig
            else if (context == "desig expression" && Reserved.dEx.Exists(d => d.desigName == parts[start]))
            {
                DesigSubExpression dse = new DesigSubExpression();
                return new IdentifyResponse(dse, expContext, dse.Parse(parts, start));
            }
            // Nonsensical answer
            throw new Exception("Inrecognized expression '" + parts[start] + "' in context '" + context + "'");
        }


        // Class gathering all file information to be made
        public class CompilationOfFiles
        {
            public class FileContents
            {
                public string filename;
                public string fileContents;


                // Directory is to datapack folder, namespace is for namespace folder
                public static void CreateFunctionFolders(string directory, string nameSpace)
                {
                    
                    // If no name is ruled
                    if (!Ruling.hasName)
                    {
                        throw new Exception("Name ruling has to be made !");
                    }

                    string dirCopy = directory;

                    /*  dir/data/namespace/functions/   */
                    // Ensure data folder is created
                    if (!Directory.Exists(directory + "/data"))
                    {
                        Directory.CreateDirectory(directory + "/data");
                    }
                    directory += "/data";

                    // Ensure function namespace folder is created
                    if (!Directory.Exists(directory + "/" + nameSpace))
                    {
                        Directory.CreateDirectory(directory + "/" + nameSpace);
                    }
                    directory += "/" + nameSpace;

                    // Ensure functions folder is created
                    if (!Directory.Exists(directory + "/functions"))
                    {
                        Directory.CreateDirectory(directory + "/functions");
                    }
                    

                    /*  dir/data/minecraft/tags/functions/   */
                    dirCopy += "/data";

                    // Ensure minecraft folder is created
                    if (!Directory.Exists(dirCopy + "/minecraft"))
                    {
                        Directory.CreateDirectory(dirCopy + "/minecraft");
                    }
                    dirCopy += "/minecraft";

                    // Ensure tags folder is created
                    if (!Directory.Exists(dirCopy + "/tags"))
                    {
                        Directory.CreateDirectory(dirCopy + "/tags");
                    }
                    dirCopy += "/tags";

                    // Ensure functions folder is created
                    if (!Directory.Exists(dirCopy + "/functions"))
                    {
                        Directory.CreateDirectory(dirCopy + "/functions");
                    }
                }
            }

            public class ConfigFile
            {
                // tick or load
                public string type;

                // namespace:functionname
                public string functionCall;

                public void Generate(string directory)
                {
                    // Ensure data folder is created
                    if (!Directory.Exists(directory + "/data"))
                    {
                        Directory.CreateDirectory(directory + "/data");
                    }
                    directory += "/data";

                    // Ensure minecraft folder is created
                    if (!Directory.Exists(directory + "/minecraft"))
                    {
                        Directory.CreateDirectory(directory + "/minecraft");
                    }
                    directory += "/minecraft";

                    // Ensure tags folder is created
                    if (!Directory.Exists(directory + "/tags"))
                    {
                        Directory.CreateDirectory(directory + "/tags");
                    }
                    
                    directory += "/tags";

                    // Ensure tags folder is created
                    if (!Directory.Exists(directory + "/functions"))
                    {
                        Directory.CreateDirectory(directory + "/functions");
                    }
                    directory += "/functions/";

                    // Now at datapackname/data/minecraft/tags/functions/
                    File.WriteAllLines(directory + type + ".json", new string[] {"{","\"values\": [", "\"" + functionCall + "\"", "]","}"});
                }
            }

            // List of functions
            List<FileContents> functions;

            // List of config files (tags of either tick or load)
            public List<ConfigFile> configs;

            // Description of datapack
            public string description;

            public CompilationOfFiles()
            {
                configs = new List<ConfigFile>();
                functions = new List<FileContents>();
            }
        }

        public static void CompileFile(string directory, string inputFile)
        {
            // Create datapack
            Directory.CreateDirectory(directory + "generated");
            
            try
            {
                // Open file
                string[] lines = File.ReadAllLines(inputFile);
                string file = "";
                foreach (string line in lines)
                {
                    file += line + "\n";
                }
                IntoAST intast = new IntoAST(file);
                
                IntoAST.CompilationOfFiles.FileContents.CreateFunctionFolders(directory + "generated\\", IntoAST.nameSpace);

                intast.CreateFiles(directory + "generated\\");
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
                Console.ReadKey();
                return;
            }
            Console.Write("Finished Compilation");
        }

        // Apply each type
        public void CreateFiles(string masterDir)
        {
            if (maintodo == null || maintodo.Count == 0)
            {
                throw new Exception("Cannot create files; Nothing specified/compiled");
            }
            else
            {
                // If no name is ruled
                if (!Ruling.hasName)
                {
                    throw new Exception("Name ruling has to be made !");
                }

                // Made a load tag function
                bool madeLoadTagFunc = false;
                // Made a description
                bool madeDesc = false;

                foreach (IChildMainFile icmf in maintodo)
                {
                    if (icmf is FunctionDeclaration fd)
                    {
                        fd.CreateFunction(masterDir + "data\\" + IntoAST.nameSpace + "\\functions\\");
                    }
                    else if (icmf is DescriptionRuling dr)
                    {
                        StreamWriter sw = new StreamWriter(masterDir + "pack.mcmeta");
                        sw.Write("{\n\t\"pack\": {\n\t\t\"pack_format\": 5,\n\t\t\"description\":\"" + dr.description + "\"\n\t}\n}");
                        sw.Close();

                        madeDesc = true;
                    }
                    //else if (icmf is DatapackNameRuling dr)
                        // Nothing is done, name ruling is for all the function's namespace
                    //else if (icmf is VariableDeclaration vd)
                    //else if (icmf is DesigDeclaration dd)
                        // Nothing is done, taken care of in the load ruling or after all IChildMainFile is taken care of
                    else if (icmf is LoadFuncRuling lf)
                    {
                        // Create load tag
                        string initString = "{\n\t\"replace\":false,\n\t\"values\":\n\t[\n\t\t\"" + IntoAST.nameSpace + ":" + lf.function + "\"\n\t]\n}";

                        StreamWriter sw = File.CreateText(masterDir + "data\\minecraft\\tags\\functions\\load.json");

                        sw.Write(initString);

                        sw.Close();
                        
                        // Has a load tagged function
                        madeLoadTagFunc = true;
                    }
                    else if (icmf is TickFuncRuling tf)
                    {
                        // Create tick tag
                        string initString = "{\n\t\"replace\":false,\n\t\"values\":\n\t[\n\t\t\"" + IntoAST.nameSpace + ":" + tf.function + "\"\n\t]\n}";

                        StreamWriter sw = File.CreateText(masterDir + "data\\minecraft\\tags\\functions\\tick.json");

                        sw.Write(initString);

                        sw.Close();
                    }
                }

                // If didn't make a load tag function
                if (!madeLoadTagFunc)
                {
                    // Get commands to initialize variables and desigs
                    string initString = Reserved.InitializeDeclarationString();

                    // Create load function initall.mcfunction
                    StreamWriter sw = File.CreateText(masterDir + "data\\" + IntoAST.nameSpace + "\\functions\\initall.mcfunction");

                    sw.Write(initString);

                    sw.Close();

                    // Create load tag
                    initString = "{\n\t\"replace\":false,\n\t\"values\":\n\t[\n\t\t\"" + IntoAST.nameSpace + ":initall\"\n\t]\n}";

                    sw = File.CreateText(masterDir + "data\\minecraft\\tags\\functions\\load.json");

                    sw.Write(initString);

                    sw.Close();
                }
                
                // If didn't make a description
                if (!madeDesc)
                {
                    StreamWriter sw = new StreamWriter(masterDir + "pack.mcmeta");
                    sw.Write("{\n\t\"pack\": {\n\t\t\"pack_format\": 5,\n\t\t\"description\":\"generated\"\n\t}\n}");
                    sw.Close();
                }
            }
        }
    }
}
