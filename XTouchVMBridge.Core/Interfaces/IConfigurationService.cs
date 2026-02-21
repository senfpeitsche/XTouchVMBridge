using XTouchVMBridge.Core.Models;

namespace XTouchVMBridge.Core.Interfaces;

public interface IConfigurationService
{
    XTouchVMBridgeConfig Load();

    void Save(XTouchVMBridgeConfig config);

    XTouchVMBridgeConfig CreateDefault();
}
