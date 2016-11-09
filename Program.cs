using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compressor
{
    class Program
    {
        static void Main(string[] args)
        {
            /*switch (args[0])
            {
                case "compress":
                    Compressor.Compress(args[1], args[2]);
                    break;
                case "decompress":
                    Compressor.Decompress(args[1], args[2]);
                    break;     
                default:
                    Console.WriteLine("incorrect command");
                    break;
            }*/
            //это пока для простоты
            Compressor.Compress("source.jpg", "out");
        }
    }
}
