using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace SmoothRun
{
    public class SmoothConfig
    {
        public bool FirstIsSpecial { get; internal set; } = true;
        public int Timeout { get; internal set; } = 5;

        internal void LoadFrom(IEnumerable<string> files)
        {
            foreach(string file in files)
            {
                XDocument xdoc = null;
                try
                {
                    xdoc = XDocument.Load(file);
                }
                catch
                {
                    continue;
                }
                Timeout = (from x in xdoc.Root.Nodes().OfType<XElement>() where x.Name == "Timeout" select int.Parse(x.Value)).Concat( new[] { Timeout }).First();
                FirstIsSpecial = (from x in xdoc.Root.Nodes().OfType<XElement>() where x.Name == "FirstIsSpecial" select bool.Parse(x.Value)).Concat( new[] { FirstIsSpecial }).First();
            }
        }
    }
}
