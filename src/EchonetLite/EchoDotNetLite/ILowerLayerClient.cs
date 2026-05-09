using System;
using System.Threading.Tasks;

namespace EchoDotNetLite
{

    public interface ILowerLayerClient
    {
        Task RequestAsync(string address, byte[] request);

        event EventHandler<(string, byte[])> OnEventReceived;
    }
}
