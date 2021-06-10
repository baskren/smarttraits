using SmartTraitsDefs;

namespace SmartTraits.Tests.Stage7
{
    [AddTrait(typeof(NameTrait))]
    [ApplyT4("DemoTemplate", Scope = T4TemplateScope.Local, VerbosityLevel = T4GeneratorVerbosity.Debug)]
    public partial class ExampleA : BaseA, IName
    {
    }

    public class BaseA { }
}
