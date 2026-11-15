namespace Asv.Sdr;

/// <summary>UVD interrogation codes. ZK4 has no information-word reply.</summary>
public enum UvdRequestCode : byte
{
    Zk1 = 1,
    Zk2 = 2,
    Zk3 = 3,
    Zk4 = 4
}
