using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasErpQuerySys.JobScheduling.Dto
{
    public class UpdatePostRequisitionQueryInput
    {
        public string bill_number { get; set; }
        public string bill_id { get; set; }
        public string bizDate { get; set; }
        public string status { get; set; }
        public string bizType_name { get; set; }


        public string entry_id { get; set; }
        public string material_model { get; set; }
        public string material_name { get; set; }
        public string material_number { get; set; }
        public string orderCustomerName { get; set; }
        public string orderCustomerNnumber { get; set; }
        public string qty { get; set; }
        public string shippedQty { get; set; }


        public string storageOrgUnit_number { get; set; }
    }
}
