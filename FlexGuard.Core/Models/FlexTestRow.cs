using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlexGuard.Core.Models
{
    public sealed class FlexTestRow
    {
        public int Id { get; init; }
        public required string TestNavn { get; init; }
    }
}
