using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Acr.UserDialogs;
using Microsoft.ProjectOxford.Vision;
using Microsoft.ProjectOxford.Vision.Contract;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using Plugin.TextToSpeech;
using Xamarin.Forms;
using XLabs.Platform.Services.Media;

namespace CognitiveServicesXamarinTest
{
    public partial class MainPage : ContentPage
    {
        private VisionServiceClient vc;
        private Stream imageStream;

        public MainPage()
        {
            InitializeComponent();
            vc = new VisionServiceClient(Constants.VISION_API_KEY);
            CrossTextToSpeech.Current.Init();
        }

        private async Task Enviar()
        {
            VisualFeature[] visualFeatures = {VisualFeature.Tags, VisualFeature.Categories, VisualFeature.Description};
            AnalysisResult ret;

            imageStream.Seek(0, SeekOrigin.Begin);
            try
            {
                Device.BeginInvokeOnMainThread(() => UserDialogs.Instance.ShowLoading("analizando imagen"));
                ret = await vc.AnalyzeImageAsync(imageStream, visualFeatures.ToList(), null);
            }
            catch (ClientException c)
            {
                Debug.WriteLine($"error al consultar servicio: {c.Error} - {c.Message} - {c}");
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"error al consultar servicio: {ex}");
                return;
            }
            finally
            {
                Device.BeginInvokeOnMainThread(() => UserDialogs.Instance.HideLoading());
            }
            if (ret.Description.Captions != null && ret.Description.Captions.Length > 0)
            {
                Resultado.Text = ret.Description.Captions[0].Text;
                try
                {
                    Device.BeginInvokeOnMainThread(() => UserDialogs.Instance.ShowLoading("traduciendo"));
                    var secret = Constants.TRANSLATOR_SECRET;
                    var clientid = Constants.TRANSLATOR_CLIENTID;
                    var turl = $"http://api.microsofttranslator.com/v2/Http.svc/Translate?text={Uri.EscapeDataString(ret.Description.Captions[0].Text)}&from=en&to=es";
                    var nurl = new Uri("https://datamarket.accesscontrol.windows.net/v2/OAuth2-13");
                    var reqDetails = new Dictionary<string, string>()
                    {
                        {"grant_type", "client_credentials"},
                        {"client_id", clientid},
                        {"client_secret", secret},
                        {"scope", "http://api.microsofttranslator.com"}
                    };
                    var content = new FormUrlEncodedContent(reqDetails);
                    var cl = new HttpClient();
                    string str;
                    try
                    {
                        var post = await cl.PostAsync(nurl, content);
                        str = await post.Content.ReadAsStringAsync();
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"excepcion: {e}");
                        return;
                    }
                    var btoken = JToken.Parse(str).Value<string>("access_token");

                    var req = new HttpRequestMessage(HttpMethod.Get, turl);
                    req.Headers.Add("Authorization", "Bearer " + btoken);
                    Stream stream;
                    try
                    {
                        var salida = await cl.SendAsync(req); //cl.GetStringAsync(turl)));
                        stream = await salida.Content.ReadAsStreamAsync();
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"excepcion: {e}");
                        return;
                    }
                    Debug.WriteLine($"traducido: {str}");
                    var dcs = new DataContractSerializer(Type.GetType("System.String"));
                    str = (string)dcs.ReadObject(stream);

                    Resultado.Text = str; //ret.Description.Captions[0].Text;

                    CrossTextToSpeech.Current.Speak(str); //ret.Description.Captions[0].Text);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"excepcion al traducir: {ex}");
                    Resultado.Text = ret.Description.Captions[0].Text;
                }
                finally
                {
                    Device.BeginInvokeOnMainThread(() => UserDialogs.Instance.HideLoading());
                }
            }
            ListaTags.ItemsSource = ret.Description.Tags;

            // subamos al mobile service
            var url = Constants.BLOB_CONTAINER;
            var key = Constants.BLOB_KEY;

            CloudBlockBlob blob;
            try
            {
                Device.BeginInvokeOnMainThread(() => UserDialogs.Instance.ShowLoading("generando blob"));
                var bc = new CloudBlobContainer(new Uri(url), new StorageCredentials(Constants.BLOB_ACCOUNT_NAME, key));
                blob = bc.GetBlockBlobReference(Guid.NewGuid() + ".jpg");
                imageStream.Seek(0, SeekOrigin.Begin);
                await blob.UploadFromStreamAsync(imageStream);
            }
            catch (Exception ex)
            {
                Device.BeginInvokeOnMainThread(() => UserDialogs.Instance.ShowError($"excepcion al subir blob: {ex}"));
                return;
            }
            finally
            {
                Device.BeginInvokeOnMainThread(() => UserDialogs.Instance.HideLoading());
            }

            Capturas cap;

            try
            {
                Device.BeginInvokeOnMainThread(() => UserDialogs.Instance.ShowLoading("Cargando en App Services"));
                var ms = new MobileServiceClient(Constants.APP_SERVICE_URI);
                var tabla = ms.GetTable<Capturas>();
                cap = new Capturas();
                cap.Descripcion = ret.Description.Captions[0].Text;
                cap.Url = blob.Uri.ToString();
                await tabla.InsertAsync(cap);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"excepcion: {ex}");
                Device.BeginInvokeOnMainThread(() => UserDialogs.Instance.ShowError($"excepcion al grabar en bd: {ex}"));
                return;
            }
            finally
            {
                Device.BeginInvokeOnMainThread(() => UserDialogs.Instance.HideLoading());
            }

            Device.BeginInvokeOnMainThread(() => UserDialogs.Instance.Alert($"Éxito! Registro creado en backend, con ID {cap.Id}"));
        }

        public class Capturas
        {
            [Newtonsoft.Json.JsonProperty("Id")]
            public string Id { get; set; }

            public string Descripcion { get; set; }

            public string Url { get; set; }
        }

        public static byte[] ReadFully(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
        private async void Capturar_OnClicked(object sender, EventArgs e)
        {
            var picker = DependencyService.Get<IMediaPicker>();
            var photo = await picker.TakePhotoAsync(new CameraMediaStorageOptions());

            // tenemos que achicar la imagen
            var bs = ReadFully(photo.Source);
            var resizer = DependencyService.Get<IImageResizer>();
            var nbs = resizer.ResizeImage(bs, 400, 400);
            var ms = new MemoryStream(nbs);
            imageStream = new MemoryStream(nbs); 
            Imagen.Source = ImageSource.FromStream(() => ms);

            await Enviar();
        }
    }
}
