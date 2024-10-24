using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleImport.Models {
    public class Provider {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string NPI { get; set; }
        public string? ProviderID { get; set; }
        public string? AppointmentID { get; set; }
        public bool? MultipleMatches { get; set; } = false;
        public bool? IsNew { get; set; } = false;
        public bool? DemographicCreated { get; set; } = false;
        public bool? AppointmentCreated { get; set; } = false;
        public int? NumberOfRequests { get; set; } = 0;
    }
}
