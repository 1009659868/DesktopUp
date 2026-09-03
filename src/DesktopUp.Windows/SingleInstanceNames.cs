using System.Security.Cryptography;
using System.Text;

namespace DesktopUp.Windows;

public sealed record SingleInstanceNames(string MutexName, string PipeName)
{
    public static SingleInstanceNames ForCurrentUser(string applicationId = "DesktopUp.V3")
    {
        string identity = $@"{Environment.UserDomainName}\{Environment.UserName}";
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..16];
        return new SingleInstanceNames(
            $@"Local\{applicationId}.{hash}.Mutex",
            $"{applicationId}.{hash}.Activation");
    }
}
