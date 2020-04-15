using System.Collections.Generic;
using TaleWorlds.SaveSystem;

namespace Recruiter
{
    public class RecruiterDataSaveableTypeDefiner : SaveableTypeDefiner
    {
        public RecruiterDataSaveableTypeDefiner() : base(42069247)
        {
        }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(RecruiterData), 1);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(Dictionary<string, RecruiterData>));
        }
    }
}
