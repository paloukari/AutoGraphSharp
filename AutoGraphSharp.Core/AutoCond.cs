using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TensorFlow;

namespace AutoGraphSharp
{
    public class AutoCond<T>
        where T : class, IStructuralEquatable, IStructuralComparable, IComparable
    {
        private AutoTFOutput _predicate;
        private Func<T> _if_true;
        private Func<T> _if_false;
        private TFSession _session;

        public int TupleRank { get; private set; }
        public IEnumerable<Type> TupleSubtypes { get; private set; }
        public T Key { get; private set; }

        public AutoCond(AutoTFOutput predicate, Func<T> if_true, Func<T> if_false, TFSession session)
        {
            TupleRank = typeof(T).GetGenericArguments().Length;
            TupleSubtypes = typeof(T).GetGenericArguments();

            _predicate = predicate;
            _if_true = if_true;
            _if_false = if_false;
            _session = session;
        }

        public void Deconstruct(out AutoTFOutput a)
        {
            var condResult = _session.Graph.Cond(_predicate, () => (_if_true() as Tuple<AutoTFOutput>).Item1, () => (_if_false() as Tuple<AutoTFOutput>).Item1);
            a = new AutoTFOutput(condResult, _session) as AutoTFOutput;
        }

        public void Deconstruct(out AutoTFOutput c, out AutoTFOutput a)
        {
            throw new NotImplementedException();
        }
    }
}
