# AutoGraphSharp

## This is the AutoGraph implementation for C# code. 

To test this code, clone the repo and open the example project. AutoGraphSharp generates the Tensorflow Graph creation code at compile time. 

### Example

This function:

        [AutoGraph]
        public static int Function(int a, int b)
        {
            var c = (a + b *3 )/a;

            if (a > b)
                c = 1;
            else
                c = 2;

            return c;
        }

will be automatically translated to this function:

        [AutoGraph]
        public static int Function(int a, int b, TensorFlow.TFSession session)
        {
            var runner = session.GetRunner();
            var _a = new AutoTFOutput(session.Graph.Placeholder(TFTensor.TensorTypeFromType(a.GetType())), session);
            runner.AddInput(_a, new TFTensor(a));
            var _b = new AutoTFOutput(session.Graph.Placeholder(TFTensor.TensorTypeFromType(b.GetType())), session);
            runner.AddInput(_b, new TFTensor(b));
            return (int)runner.Run(_Function(_a, _b, session)).GetValue();
        }

        [AutoGraph]
        public static AutoTFOutput _Function(AutoTFOutput a, AutoTFOutput b, TensorFlow.TFSession session)
        {
            var c = (a + b * 3) / a;
            var predicate1 = a > b;
            Func<Tuple<AutoTFOutput>> ifTrue1 = () =>
            {
                var _c = c;
                _c = new AutoTFOutput(session.Graph.Const(1), session);
                return Tuple.Create(_c);
            };
            Func<Tuple<AutoTFOutput>> ifFalse1 = () =>
            {
                var _c = c;
                _c = new AutoTFOutput(session.Graph.Const(2), session);
                return Tuple.Create(_c);
            };
            var res = new AutoCond<Tuple<AutoTFOutput>>(predicate1, ifTrue1, ifFalse1, session);
            res.Deconstruct(out c);
            return c;
        }
        
>Note: This is an incomplete work in progress. The first goal if to implement a generic **if statement** Graph translation that will work for any C# **if** permutation. Once there, the rest of the C# language features will follow (while loops, switch cases etc).
