using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Microsoft.VisualBasic.FileIO;
using System.Windows.Forms;
using GH_IO.Serialization;
using System.IO;

namespace CSVulture
{
    public class CSVReader : GH_Component, IGH_VariableParameterComponent
    {
        private bool rows = false;

        List<string> uniqueColumnNames;
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public CSVReader()
          : base("Read CSV", "CSV",
            "Read data from a .CSV file.",
            "CSVulture", "Data")
        {
            uniqueColumnNames = new List<string>();
            UpdateMessage();
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendItem(menu, "Outputs By Row", Menu_ParseByRow_Clicked,true,rows);
            base.AppendAdditionalComponentMenuItems(menu);
        }

        private void Menu_ParseByRow_Clicked(object sender, EventArgs e)
        {
            rows = !rows;
            UpdateMessage();
            ExpireSolution(true);
        }

        private void UpdateMessage()
        {
            Message = rows ? "Rows" : "Columns";
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("CSV Path", "C", "The CSV filepath or raw data", GH_ParamAccess.item);
            pManager.AddTextParameter("Delimiter", "D", "The character to use as a delimeter — comma by default", GH_ParamAccess.item, ",");
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {

        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var csvPath = "";
            DA.GetData("CSV Path", ref csvPath);
            var delim = ",";
            DA.GetData("Delimiter", ref delim);

           


            if (DA.Iteration == 0)
            {
                var allData = Params.Input.OfType<Param_String>()
               .First()
               .VolatileData.AllData(true)
               .OfType<GH_String>()
               .Select(s => GetSheet(s.Value, delim));
                if (!allData.Any())
                {
                    return;
                }

                var firstCells = allData.SelectMany(table => table.Select(row => row.FirstOrDefault()));

                uniqueColumnNames = new List<string>();

                foreach (var property in firstCells)
                {
                    if (!uniqueColumnNames.Contains(property))
                    {
                        uniqueColumnNames.Add(property);
                    }
                }

                var names = firstCells.Distinct().ToList();
            }

            if (uniqueColumnNames.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No valid columns found");
            }


            if (OutputMismatch() && DA.Iteration == 0)
            {
                OnPingDocument().ScheduleSolution(5, d =>
                {
                    AutoCreateOutputs(false);
                });
            }
            else
            {
                var sheet = GetSheet(csvPath, delim);
                if (sheet == null)
                {
                    return;
                }

                for (var i = 0; i < sheet.Count; i++)
                {
                    var row = sheet[i];

                    var name = row[0];
                    row.RemoveAt(0);
                    DA.SetDataList(name, row);

                }
            }
        }

        private List<List<string>> GetSheet(string path, string delimiter)
        {

            if (!File.Exists(path))
            {
                var tempPath = Path.GetTempFileName();
                File.WriteAllText(tempPath, path);
                path = tempPath;
            }


            List<List<string>> results = new List<List<string>>();
            using (TextFieldParser parser = new TextFieldParser(path))
            {
                parser.SetDelimiters(delimiter);
                while (!parser.EndOfData)
                {
                    //Processing row

                    string[] fields = null;
                    try
                    {
                        fields = parser.ReadFields();
                    }
                    catch (MalformedLineException ex)
                    {
                        if (parser.ErrorLine.StartsWith("\""))
                        {
                            var line = parser.ErrorLine.Substring(1, parser.ErrorLine.Length - 2);
                            fields = line.Split(new string[] { "\",\"" }, StringSplitOptions.None);
                        }
                        else
                        {
                            throw;
                        }
                    }


                    if (rows)
                    {
                        results.Add(fields.ToList());
                    } else
                    {
                        for (int i = 0; i < fields.Length; i++)
                        {
                            if(results.Count == i)
                            {
                                results.Add(new List<string>());
                            }
                            results[i].Add(fields[i]);
                        }
                    }
                }
            }
            return results;
        }

        private bool OutputMismatch()
        {
            var countMatch = uniqueColumnNames.Count == Params.Output.Count;
            if (!countMatch) return true;

            for (int i = 0; i < uniqueColumnNames.Count; i++)
            {
                string name = uniqueColumnNames[i];
                if (Params.Output[i].Name != name)
                {
                    return true;
                }
            }

            return false;
        }

        private void AutoCreateOutputs(bool recompute)
        {

            var tokenCount = uniqueColumnNames.Count;
            if (tokenCount == 0) return;

            if (OutputMismatch())
            {
                RecordUndoEvent("Output update from CSV change");
                if (Params.Output.Count < tokenCount)
                {
                    while (Params.Output.Count < tokenCount)
                    {
                        var new_param = CreateParameter(GH_ParameterSide.Output, Params.Output.Count);
                        Params.RegisterOutputParam(new_param);
                    }
                }
                else if (Params.Output.Count > tokenCount)
                {
                    while (Params.Output.Count > tokenCount)
                    {
                        Params.UnregisterOutputParameter(Params.Output[Params.Output.Count - 1]);
                    }
                }
                Params.OnParametersChanged();
                VariableParameterMaintenance();
                ExpireSolution(recompute);
            }
        }



        public bool CanInsertParameter(GH_ParameterSide side, int index)
        {
            return false;
        }

        public bool CanRemoveParameter(GH_ParameterSide side, int index)
        {
            return false;
        }

        public IGH_Param CreateParameter(GH_ParameterSide side, int index)
        {
            return new Param_GenericObject();
        }

        public bool DestroyParameter(GH_ParameterSide side, int index)
        {
            return true;
        }

        public void VariableParameterMaintenance()
        {
            var names = uniqueColumnNames;
            for (var i = 0; i < Params.Output.Count; i++)
            {
                if (i > names.Count - 1) return;
                var name = names[i];
                Params.Output[i].Name = $"{name}";
                Params.Output[i].NickName = $"{name}";
                Params.Output[i].Description = $"Data from column: {name}";
                Params.Output[i].MutableNickName = false;
                Params.Output[i].Access = GH_ParamAccess.list;

            }
        }


        public override bool Read(GH_IReader reader)
        {
            uniqueColumnNames = new List<string>();
            reader.TryGetBoolean(nameof(rows), ref rows);
            return base.Read(reader);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetBoolean(nameof(rows), rows);
            return base.Write(writer);
        }

        /// <summary>
        /// The Exposure property controls where in the panel a component icon 
        /// will appear. There are seven possible locations (primary to septenary), 
        /// each of which can be combined with the GH_Exposure.obscure flag, which 
        /// ensures the component will only be visible on panel dropdowns.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.primary;


        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                return Resources.Read;
            }
        }

        public override Guid ComponentGuid => new Guid("396E2D79-86A3-4D60-B6E0-B72D8B1BD12C");
    }
}