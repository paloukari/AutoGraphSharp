using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using TensorFlow;

namespace AutoGraphSharp
{
    public partial class AutoTFOutput
    {
        TFSession _session;
        TFTensor _value;
        TFOutput? _output;

        public AutoTFOutput(TFTensor value)
        {
            _value = value;
        }
        public AutoTFOutput(TFTensor value, TFSession session)
        {
            _session = session;
            _value = value;
        }

        public AutoTFOutput(TFOutput output, TFSession session)
        {
            _session = session;
            _output = output;
        }

        public TFOutput Output
        {
            get
            {
                if (_output.HasValue)
                    return _output.Value;
                _output = _session.Graph.Const(_value);
                return _output.Value;
            }
        }

        private static TFSession GetSession(AutoTFOutput a, AutoTFOutput b)
        {
            if (a._session == null && b._session == null)
                throw new Exception("Cannot calculate graph of two non-tensor values. At least one operant has to be a Tensor");
            if (a._session == null)
                a._session = b._session;
            if (b._session == null)
                b._session = a._session;
            return a._session;
        }

        public override bool Equals(object obj)
        {
            var output = obj as AutoTFOutput;
            return Output.Equals(output.Output);
        }

        public static AutoTFOutput operator +(AutoTFOutput a, AutoTFOutput b)
        {
            var session = GetSession(a, b);
            return new AutoTFOutput(session.Graph.Add(a, b), session);
        }
        public static AutoTFOutput operator -(AutoTFOutput a, AutoTFOutput b)
        {
            var session = GetSession(a, b);
            return new AutoTFOutput(session.Graph.Sub(a, b), session);
        }
        public static AutoTFOutput operator *(AutoTFOutput a, AutoTFOutput b)
        {
            var session = GetSession(a, b);
            return new AutoTFOutput(session.Graph.Mul(a, b), session);
        }
        public static AutoTFOutput operator /(AutoTFOutput a, AutoTFOutput b)
        {
            var session = GetSession(a, b);
            return new AutoTFOutput(session.Graph.Div(a, b), session);
        }
        public static AutoTFOutput operator %(AutoTFOutput a, AutoTFOutput b)
        {
            var session = GetSession(a, b);
            return new AutoTFOutput(session.Graph.Mod(a, b), session);
        }

        public static AutoTFOutput operator >(AutoTFOutput a, AutoTFOutput b)
        {
            var session = GetSession(a, b);
            return new AutoTFOutput(session.Graph.Greater(a, b), session);
        }

        public static AutoTFOutput operator >=(AutoTFOutput a, AutoTFOutput b)
        {
            var session = GetSession(a, b);
            return new AutoTFOutput(session.Graph.GreaterEqual(a, b), session);
        }

        public static AutoTFOutput operator <(AutoTFOutput a, AutoTFOutput b)
        {
            var session = GetSession(a, b);
            return new AutoTFOutput(session.Graph.Less(a, b), session);
        }

        public static AutoTFOutput operator <=(AutoTFOutput a, AutoTFOutput b)
        {
            var session = GetSession(a, b);
            return new AutoTFOutput(session.Graph.LessEqual(a, b), session);
        }

        public static AutoTFOutput operator ==(AutoTFOutput a, AutoTFOutput b)
        {
            var session = GetSession(a, b);
            return new AutoTFOutput(session.Graph.Equal(a, b), session);
        }
        public static AutoTFOutput operator !=(AutoTFOutput a, AutoTFOutput b)
        {
            var session = GetSession(a, b);
            return new AutoTFOutput(session.Graph.NotEqual(a, b), session);
        }
        public static AutoTFOutput operator !(AutoTFOutput a)
        {
            return new AutoTFOutput(a._session.Graph.LogicalNot(a), a._session);
        }

        public static AutoTFOutput operator ++(AutoTFOutput a)
        {
            return new AutoTFOutput(a._session.Graph.Add(a, a._session.Graph.Const(1)), a._session);
        }
        public static AutoTFOutput operator --(AutoTFOutput a)
        {
            return new AutoTFOutput(a._session.Graph.Add(a, a._session.Graph.Const(-1)), a._session);
        }
        public static implicit operator TFOutput(AutoTFOutput a)
        {
            return a.Output;
        }
        public static implicit operator AutoTFOutput(TFTensor tensor)
        {
            return new AutoTFOutput(tensor);
        }
        public static implicit operator AutoTFOutput(Array array)
        {
            return new AutoTFOutput(array);
        }
        public static implicit operator AutoTFOutput(Complex value)
        {
            return new AutoTFOutput(value);
        }
        public static implicit operator AutoTFOutput(float value)
        {
            return new AutoTFOutput(value);
        }
        public static implicit operator AutoTFOutput(double value)
        {
            return new AutoTFOutput(value);
        }
        public static implicit operator AutoTFOutput(long value)
        {
            return new AutoTFOutput(value);
        }
        public static implicit operator AutoTFOutput(bool value)
        {
            return new AutoTFOutput(value);
        }
        public static implicit operator AutoTFOutput(int value)
        {
            return new AutoTFOutput(value);
        }
        public static implicit operator AutoTFOutput(byte value)
        {
            return new AutoTFOutput(value);
        }
    }
}
