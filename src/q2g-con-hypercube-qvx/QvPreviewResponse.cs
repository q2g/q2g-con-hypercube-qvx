using QlikView.Qvx.QvxLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static q2gconhypercubemain.TableFunc;

namespace q2gconhypercubeqvx
{
    public class PreviewResponse : QvDataContractResponse
    {
        public List<PreviewRow> qPreview { get; set; }
        public int MaxCount { get; set; } = 15;
        public PreviewResponse()
        {
            this.qPreview = new List<PreviewRow>();
        }
    }
}
