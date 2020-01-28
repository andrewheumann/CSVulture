using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace CSVulture
{
    public class CSVultureInfo : GH_AssemblyInfo
    {
        public override string Name => "CSVulture";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "A set of tools for working with CSV and other delimited data sets";

        public override Guid Id => new Guid("315ED962-AED1-4B61-9888-DB302891165F");

        //Return a string identifying you or your company.
        public override string AuthorName => "Andrew Heumann";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "https://discourse.mcneel.com/c/grasshopper/human/88";

        public override string Version => "1.0.1";
    }
}