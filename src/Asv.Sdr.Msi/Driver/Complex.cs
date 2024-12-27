using System;

namespace Asv.Sdr.Msi
{
    public struct Complex
    {
        public float Real;

        public float Imag;

        public Complex(float real, float imaginary)
        {
            this.Real = real;
            this.Imag = imaginary;
        }

        public Complex(Complex c)
        {
            this.Real = c.Real;
            this.Imag = c.Imag;
        }

        public float Modulus()
        {
            return (float)Math.Sqrt((double)this.ModulusSquared());
        }

        public float ModulusSquared()
        {
            return (this.Real * this.Real) + (this.Imag * this.Imag);
        }

        public float Argument()
        {
            return (float)Math.Atan2((double)this.Imag, (double)this.Real);
        }

        public float ArgumentFast()
        {
            return Trig.Atan2(this.Imag, this.Real);
        }

        public Complex Conjugate()
        {
            Complex result = default(Complex);
            result.Real = this.Real;
            result.Imag = 0f - this.Imag;
            return result;
        }

        public Complex Normalize()
        {
            float b = 1f / this.Modulus();
            return this * b;
        }

        public Complex NormalizeFast()
        {
            float b = 1.95f - this.ModulusSquared();
            return this * b;
        }

        public override string ToString()
        {
            return string.Format("real {0}, imag {1}", this.Real, this.Imag);
        }

        public static Complex FromAngle(double angle)
        {
            Complex result = default(Complex);
            result.Real = (float)Math.Cos(angle);
            result.Imag = (float)Math.Sin(angle);
            return result;
        }

        public static Complex FromAngleFast(float angle)
        {
            return Trig.SinCos(angle);
        }

        public static bool operator ==(Complex leftHandSide, Complex rightHandSide)
        {
            if (Math.Abs(leftHandSide.Real - rightHandSide.Real) > float.Epsilon)
            {
                return false;
            }

            return Math.Abs(leftHandSide.Imag - rightHandSide.Imag) < float.Epsilon;
        }

        public static bool operator !=(Complex leftHandSide, Complex rightHandSide)
        {
            if (Math.Abs(leftHandSide.Real - rightHandSide.Real) > float.Epsilon)
            {
                return true;
            }

            return Math.Abs(leftHandSide.Imag - rightHandSide.Imag) > float.Epsilon;
        }

        public static Complex operator +(Complex a, Complex b)
        {
            Complex result = default(Complex);
            result.Real = a.Real + b.Real;
            result.Imag = a.Imag + b.Imag;
            return result;
        }

        public static Complex operator -(Complex a, Complex b)
        {
            Complex result = default(Complex);
            result.Real = a.Real - b.Real;
            result.Imag = a.Imag - b.Imag;
            return result;
        }

        public static Complex operator *(Complex a, Complex b)
        {
            Complex result = default(Complex);
            result.Real = (a.Real * b.Real) - (a.Imag * b.Imag);
            result.Imag = (a.Imag * b.Real) + (a.Real * b.Imag);
            return result;
        }

        public static Complex operator *(Complex a, float b)
        {
            Complex result = default(Complex);
            result.Real = a.Real * b;
            result.Imag = a.Imag * b;
            return result;
        }

        public static Complex operator /(Complex a, Complex b)
        {
            float num = (b.Real * b.Real) + (b.Imag * b.Imag);
            num = 1f / num;
            Complex result = default(Complex);
            result.Real = ((a.Real * b.Real) + (a.Imag * b.Imag)) * num;
            result.Imag = ((a.Imag * b.Real) - (a.Real * b.Imag)) * num;
            return result;
        }

        public static Complex operator /(Complex a, float b)
        {
            b = 1f / b;
            Complex result = default(Complex);
            result.Real = a.Real * b;
            result.Imag = a.Imag * b;
            return result;
        }

        public static Complex operator ~(Complex a)
        {
            return a.Conjugate();
        }

        public static implicit operator Complex(float d)
        {
            return new Complex(d, 0f);
        }

        public override int GetHashCode()
        {
            return this.Real.GetHashCode() * 397 ^ this.Imag.GetHashCode();
        }

        public bool Equals(Complex obj)
        {
            if (Math.Abs(obj.Real - this.Real) < float.Epsilon)
            {
                return Math.Abs(obj.Imag - this.Imag) < float.Epsilon;
            }

            return false;
        }

        public override bool Equals(object? obj)
        {
            return obj?.GetType() == typeof(Complex) && this.Equals((Complex)obj);
        }
    }
}
