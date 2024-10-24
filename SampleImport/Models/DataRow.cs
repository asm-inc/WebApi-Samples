using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleImport.Models {
    public class DataRow {
        public Guid ImportRowID { get; set; } = Guid.NewGuid();
        public Provider? Provider { get; set; }
        public Insurance? Insurance { get; set; }
    }
}
