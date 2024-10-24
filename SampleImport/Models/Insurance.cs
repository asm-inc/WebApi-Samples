using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleImport.Models {
    public class Insurance {
        public string InsuranceCarrier { get; set; }
        public string? Address { get; set; }
        public string? Address2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public string? Country { get; set; }
        public string? Telephone { get; set; }
        public string? PolicyNumber { get; set; }
        public string? Coverage { get; set; }
        public string? IssueDate { get; set; }
        public string? Expires { get; set; }
        public string? InsuranceID { get; set; }
        public string? ReferenceSourceID { get; set; }
        public string? ProviderID { get; set; }
        public bool? IsNew { get; set; } = false;
        public bool? ReferenceSourceRecordCreated { get; set; } = false;
        public bool? InsuranceRecordCreated { get; set; } = false;
        public int? NumberOfRequests { get; set; } = 0;
    }
}
