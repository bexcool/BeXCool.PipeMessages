using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeXCool.PipeMessages.Tests
{
    public class TestModel
    {
        public int Id { get; set; } = 1;
        public string Data { get; set; } = "1234";

        public TestModel() { }
        public TestModel(int id, string data)
        {
            Id = id;
            Data = data;
        }
    }
}
