using System;

namespace ProjectOrigin.Wallet.Server.Models.Primitives
{
    public class Period
    {
        public DateTime DateFrom { get; }
        public DateTime DateTo { get; }

        public Period(DateTime dateFrom, DateTime dateTo)
        {
            if(dateFrom >= dateTo)
                throw new ArgumentException("DateFrom must be smaller than DateTo");

            DateFrom = dateFrom;
            DateTo = dateTo;
        }
    }
}
