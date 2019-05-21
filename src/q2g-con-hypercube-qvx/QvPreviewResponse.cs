using NLog;
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
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        public List<PreviewRow> qPreview { get; set; }
        public int MaxCount { get; set; } = 15;
        public PreviewResponse()
        {
            this.qPreview = new List<PreviewRow>();
        }

        public static PreviewResponse Create(IPreviewResponse preview)
        {
            try
            {
                var result = new PreviewResponse()
                {
                    MaxCount = preview.MaxCount,
                };
                result.qPreview.AddRange(preview.qPreview);
                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The preview result could not create.");
                return null;
            }
        }
    }
}
