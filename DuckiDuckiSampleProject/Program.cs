using DuckiDuckiCastingLib.Annotations;

namespace DuckiDuckiSampleProject
{
    [SupportsDuckCast]
    public partial interface IWorker
    {
        void DoWork();
    }

    public class Imposter
    {
        public void DoWork()
        {
            Console.WriteLine("Doing imposter things...");
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            IWorker worker = IWorker.Cast(new Imposter());
            worker.DoWork();
        }
    }
}
