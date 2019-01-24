using System;

namespace DotNedSaturday.Dto
{
    public class LeaseCarGetQuoteArgs
    {
        public LeaseCarGetQuoteArgs(Guid employeeId, string dealerName)
        {
            EmployeeId = employeeId;
            DealerName = dealerName;
        }
        public Guid EmployeeId { get; set; }
        public string DealerName { get; set; }
    }
}