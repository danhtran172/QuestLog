using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterPlay.Models.Models
{
    public class Selections
    {
        public List<SelectionItem> Genre { get; set; }
        public List<SelectionItem> Theme { get; set; }
        public List<string> Progress { get; set; }
        public List<string> Ownership { get; set; }
    }
    public class SelectionItem
    {
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
}
