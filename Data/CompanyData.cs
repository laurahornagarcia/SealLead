using System;
using System.Collections.Generic;
using System.Text;

namespace SealLead.Data
{
    public class CompanyData
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string ProfileUrl { get; set; } = "";
        public string Address { get; set; } = "";
        public string LegalName { get; set; } = "";
        public string Cif { get; set; } = "";
        public string LegalForm { get; set; } = "";
        public string Sector { get; set; } = "";
        public string Activity { get; set; } = "";
        public string CnaeActivity { get; set; } = "";
        public string SearchKeywords { get; set; } = "";

    }
}
