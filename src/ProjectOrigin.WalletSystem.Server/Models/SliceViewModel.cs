using System;

namespace ProjectOrigin.WalletSystem.Server.Models
{
    public class SliceViewModel
    {
        public Guid SliceId { get; set; }
        public long Quantity { get; set; }

        public override bool Equals(object? obj)
        {
            if(obj == null) return false;
            var b = obj as SliceViewModel;
            return SliceId.Equals(b!.SliceId);
        }

        public override int GetHashCode()
        {
            return SliceId.GetHashCode() * Quantity.GetHashCode();
        }
    }
}
