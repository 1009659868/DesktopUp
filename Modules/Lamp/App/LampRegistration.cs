using System.Runtime.CompilerServices;
using DesktopUp.Domain;
namespace DesktopUp;
internal static class LampRegistration
{
    [ModuleInitializer]
    internal static void Register() => SceneModuleRegistry.Register(TemplateParameterSchemas.LampCapability, () => new LampSceneInstance(), ControlCenterWindow.CreateLampSampleArt);
}
