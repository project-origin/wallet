namespace ProjectOrigin.WalletSystem.Server.Models;

public enum SliceState
{
    Available = 1,
    Slicing = 2, // Reserved
    Registering = 3,
    Sliced = 4,
    Transferred = 5,
    Claimed = 7,
    Reserved = 10,
}
