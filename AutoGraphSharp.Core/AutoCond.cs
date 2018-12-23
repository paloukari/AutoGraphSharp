using System;
using System.Collections.Generic;
using System.Text;
using TensorFlow;

namespace AutoGraphSharp
{
    public class AutoCond
    {
        private AutoTFOutput _predicate;
        private Func<AutoTFOutput> _if_true;
        private Func<AutoTFOutput> _if_false;
        private TFSession _session;

        public AutoCond(AutoTFOutput predicate, Func<AutoTFOutput> if_true, Func<AutoTFOutput> if_false, TFSession session)
        {
            _predicate = predicate;
            _if_true = if_true;
            _if_false = if_false;
            _session = session;
        }

        public static implicit operator AutoTFOutput(AutoCond a)
        {
            return new AutoTFOutput(a._session.Graph.Cond(a._predicate,  () => a._if_true().Output, () => a._if_false().Output), a._session);
        }
    }
}
