using System;

namespace MCFCompiler
{
    /**/
    public static class Ruling
    {
        public static bool hasDesc = false;
        public static bool hasName = false;
        public static bool hasLoad = false;
        public static bool hasTick = false;

        public static RulingResponse IdentifyRuling(string[] parts, int start)
        {
            RulingResponse rr = new RulingResponse();

            if (parts[start] != "rule")
            {
                throw new Exception("Ruling started with '" + parts[start] + "' not 'rule'!");
            }
            else if (parts[start + 1] == "description")
            {
                DescriptionRuling dr = new DescriptionRuling();
                rr.icmf = dr;
                rr.start = dr.Parse(parts, start);

                if (hasDesc)
                {
                    throw new Exception("Description ruling is already defined!");
                }
                hasDesc = true;

                return rr;
            }
            else if (parts[start + 1] == "name")
            {
                DatapackNameRuling dnr = new DatapackNameRuling();
                rr.icmf = dnr;
                rr.start = dnr.Parse(parts, start);

                if (hasName)
                {
                    throw new Exception("Name ruling is already defined!");
                }
                hasName = true;

                return rr;
            }
            else if (parts[start + 1] == "load")
            {
                LoadFuncRuling cfr = new LoadFuncRuling();
                rr.icmf = cfr;
                rr.start = cfr.Parse(parts, start);
                
                if (hasLoad)
                {
                    throw new Exception("Load ruling is already defined!");
                }
                hasLoad = false;

                return rr;
            }
            else if (parts[start + 1] == "tick")
            {
                TickFuncRuling cfr = new TickFuncRuling();
                rr.icmf = cfr;
                rr.start = cfr.Parse(parts, start);
                
                if (hasTick)
                {
                    throw new Exception("Tick ruling is already defined!");
                }
                hasTick = false;

                return rr;
            }
            else
            {
                throw new Exception("Ruling '" + parts[start] + "' is not recognized!");
            }
        }
    }

    public class RulingResponse
    {
        public IChildMainFile icmf;
        public int start;

        public RulingResponse() {}
    }
    /**/

    // Defines datapack description
    class DescriptionRuling : IChildMainFile
    {
        public string description;

        public int Parse(string[] parts, int start)
        {
            description = "";
            if (parts[start] != "rule")
            {
                throw new Exception("Description ruling started with '" + parts[start] + "' not 'rule'!");
            }
            else if (parts[start + 1] != "description")
            {
                throw new Exception("Description ruling started with 'rule " + parts[start] + "' not 'rule description'!");
            }
            else
            {
                // Start at beginning of description
                start += 2;
                while (parts[start] != ";")
                {
                    description += parts[start];

                    // Add spaces between the parts
                    if (parts[start + 1] != ";")
                    {
                        description += " ";
                    }
                    
                    start++;
                }

                // Return position after ;
                return start + 1;
            }
        }
    }

    // Defines datapack namespace
    class DatapackNameRuling : IChildMainFile
    {
        public string dpName;

        public int Parse(string[] parts, int start)
        {
            dpName = "";
            if (parts[start] != "rule")
            {
                throw new Exception("Datapack name ruling started with '" + parts[start] + "' not 'rule'!");
            }
            else if (parts[start + 1] != "name")
            {
                throw new Exception("Datapack name ruling started with 'rule " + parts[start + 1] + "' not 'rule name'!");
            }
            else if (parts[start + 3] != ";")
            {
                throw new Exception("Datapack name ruling ended with '" + parts[start + 3] + "' not ';'!");
            }
            else
            {
                // Name
                dpName = parts[start + 2];

                IntoAST.nameSpace = dpName;

                // Return position after ;
                return start + 4;
            }
        }
    }

    // Defines load tag for function
    class LoadFuncRuling : IChildMainFile
    {
        public string function;

        public int Parse(string[] parts, int start)
        {
            function = "";
            // If starts with rule
            if (parts[start] != "rule")
            {
                throw new Exception("Load ruling started with '" + parts[start] + "' not 'rule'!");
            }
            // If continues with load
            else if (parts[start + 1] != "load")
            {
                throw new Exception("Load ruling started with 'rule " + parts[start + 1] + "' not 'rule load'!");
            }
            // If function name exists
            else if (!Reserved.fEx.Exists(f => f.funcName == parts[start + 2]))
            {
                throw new Exception(parts[start + 2] + " is not a function!");
            }
            // If ends with ;
            else if (parts[start + 3] != ";")
            {
                throw new Exception("Load ruling ended with '" + parts[start + 3] + "' not ';'!");
            }
            else
            {
                function = parts[start + 2];

                // Return position after ;
                return start + 4;
            }
        }
    }

    // Defines load tag for function
    class TickFuncRuling : IChildMainFile
    {
        public string function;

        public int Parse(string[] parts, int start)
        {
            function = "";
            // If starts with rule
            if (parts[start] != "rule")
            {
                throw new Exception("Tick ruling started with '" + parts[start] + "' not 'rule'!");
            }
            // If continues with load
            else if (parts[start + 1] != "tick")
            {
                throw new Exception("Tick ruling started with 'rule " + parts[start + 1] + "' not 'rule tick'!");
            }
            // If function name exists
            else if (!Reserved.fEx.Exists(f => f.funcName == parts[start + 2]))
            {
                throw new Exception(parts[start + 2] + " is not a function!");
            }
            // If ends with ;
            else if (parts[start + 3] != ";")
            {
                throw new Exception("Tick ruling ended with '" + parts[start + 3] + "' not ';'!");
            }
            else
            {
                function = parts[start + 2];

                // Return position after ;
                return start + 4;
            }
        }
    }
}