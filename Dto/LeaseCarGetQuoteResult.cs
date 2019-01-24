namespace DotNedSaturday.Dto
{
    public class LeaseCarGetQuoteResult
    {
        public LeaseCarGetQuoteResult(string dealerName, double quote)
        {
            DealerName = dealerName;
            Quote = quote;
        }
        public string DealerName { get; set; }
        public double Quote { get; set; }
    }
}