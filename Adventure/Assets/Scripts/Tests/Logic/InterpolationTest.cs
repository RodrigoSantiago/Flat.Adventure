using System;
using Adventure.Logic;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Logic {
	public class InterpolationTest {

		private const float delta = 0.00001f;

		private static float[] values = {
			0.0f, 0.1f, 0.25f, 0.5f, 0.75f, 0.9f, 1.0f
		};

		[Test]
		public void Linear() {
			float[] arrange = {0f, 0.1f, 0.25f, 0.5f, 0.75f, 0.9f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Linear(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Smooth() {
			float[] arrange = {0f, 0.028f, 0.15625f, 0.5f, 0.84375f, 0.972f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Smooth(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Smooth2() {
			float[] arrange = {0f, 0.002308096f, 0.06561279f, 0.5f, 0.9343872f, 0.9976919f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Smooth2(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Smoother() {
			float[] arrange = {0f, 0.00856f, 0.1035156f, 0.5f, 0.8964844f, 0.99144f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Smoother(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Pow2() {
			float[] arrange = {0f, 0.02f, 0.125f, 0.5f, 0.875f, 0.98f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Pow2(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Pow2In() {
			float[] arrange = {0f, 0.01f, 0.0625f, 0.25f, 0.5625f, 0.8099999f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Pow2In(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Pow2Out() {
			float[] arrange = {0f, 0.1900001f, 0.4375f, 0.75f, 0.9375f, 0.99f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Pow2Out(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Pow2InInverse() {
			float[] arrange = {0f, 0.3162278f, 0.5f, 0.7071068f, 0.8660254f, 0.9486833f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Pow2InInverse(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Pow2OutInverse() {
			float[] arrange = {0f, 0.05131674f, 0.1339746f, 0.2928932f, 0.5f, 0.6837722f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Pow2OutInverse(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Pow3() {
			float[] arrange = {0f, 0.004f, 0.0625f, 0.5f, 0.9375f, 0.996f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Pow3(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Pow3In() {
			float[] arrange = {0f, 0.001f, 0.015625f, 0.125f, 0.421875f, 0.7289999f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Pow3In(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Pow3Out() {
			float[] arrange = {0f, 0.2710001f, 0.578125f, 0.875f, 0.984375f, 0.999f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Pow3Out(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Pow3InInverse() {
			float[] arrange = {0f, 0.4641589f, 0.6299605f, 0.7937005f, 0.9085603f, 0.9654894f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Pow3InInverse(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Pow3OutInverse() {
			float[] arrange = {0f, 0.03451061f, 0.09143972f, 0.2062995f, 0.3700395f, 0.535841f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Pow3OutInverse(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Pow4() {
			float[] arrange = {0f, 0.0008f, 0.03125f, 0.5f, 0.96875f, 0.9992f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Pow4(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Pow4In() {
			float[] arrange = {0f, 0.0001f, 0.00390625f, 0.0625f, 0.3164063f, 0.6560999f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Pow4In(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Pow4Out() {
			float[] arrange = {0f, 0.3439001f, 0.6835938f, 0.9375f, 0.9960938f, 0.9999f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Pow4Out(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Pow5() {
			float[] arrange = {0f, 0.00016f, 0.015625f, 0.5f, 0.984375f, 0.99984f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Pow5(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Pow5In() {
			float[] arrange = {0f, 1E-05f, 0.0009765625f, 0.03125f, 0.2373047f, 0.5904899f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Pow5In(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Pow5Out() {
			float[] arrange = {0f, 0.4095101f, 0.7626953f, 0.96875f, 0.9990234f, 0.99999f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Pow5Out(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Exp10() {
			float[] arrange = {0f, 0.001466276f, 0.01515152f, 0.5f, 0.9848485f, 0.9985337f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Exp10(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Exp10In() {
			float[] arrange = {0f, 0.0009775171f, 0.004552155f, 0.03030303f, 0.175972f, 0.4995112f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Exp10In(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Exp10Out() {
			float[] arrange = {9.313226E-10f, 0.5004888f, 0.824028f, 0.969697f, 0.9954479f, 0.9990225f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Exp10Out(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Exp5() {
			float[] arrange = {0f, 0.01612903f, 0.07511055f, 0.5f, 0.9248894f, 0.983871f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Exp5(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Exp5In() {
			float[] arrange = {0f, 0.01336173f, 0.04446497f, 0.1502211f, 0.401753f, 0.6976585f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Exp5In(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Exp5Out() {
			float[] arrange = {2.980232E-08f, 0.3023414f, 0.598247f, 0.8497789f, 0.9555351f, 0.9866382f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Exp5Out(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Sine() {
			float[] arrange = {0f, 0.02447173f, 0.1464466f, 0.5f, 0.8535534f, 0.9755282f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Sine(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void SineIn() {
			float[] arrange = {0f, 0.01231164f, 0.0761205f, 0.2928932f, 0.6173166f, 0.8435655f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.SineIn(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void SineOut() {
			float[] arrange = {0f, 0.1564345f, 0.3826835f, 0.7071068f, 0.9238795f, 0.9876884f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.SineOut(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void Circle() {
			float[] arrange = {0f, 0.01010206f, 0.06698731f, 0.5f, 0.9330127f, 0.989898f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.Circle(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void CircleIn() {
			float[] arrange = {0f, 0.005012572f, 0.03175414f, 0.1339746f, 0.3385622f, 0.56411f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.CircleIn(values[i]), arrange[i], delta);
			}
		}

		[Test]
		public void CircleOut() {
			float[] arrange = {0f, 0.4358899f, 0.6614378f, 0.8660254f, 0.9682459f, 0.9949874f, 1f};
			for (int i = 0; i < values.Length; i++) {
				Assert.AreEqual(Interpolation.CircleOut(values[i]), arrange[i], delta);
			}
		}
	}
}