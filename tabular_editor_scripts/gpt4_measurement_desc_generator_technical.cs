using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;
using System.IO;

// You need to signin to https://platform.openai.com/ and create an API key for your profile then paste that key 

const string apiKeyFilePath = "API_KEY_PATH";
const string uri = "https://api.openai.com/v1/chat/completions";

const int sleepTime = 20000; // the number of milliseconds
const int apiLimit = 400;    
const bool dontOverwrite = false; // this prevents existing descriptions from being overwritten
const bool debug = true;

string apiKey = File.ReadAllText(apiKeyFilePath);

using (var client = new HttpClient()) {
    client.DefaultRequestHeaders.Clear();
    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);

    int callCount = 0;
    
    // if any measures are currently selected add those to our collection
    List<Measure> myMeasures = new List<Measure>();
    myMeasures.AddRange( Selected.Measures );

    // if no measures were selected grab all of the measures in the model
    if ( myMeasures.Count == 0)
    {
       myMeasures.AddRange(Model.Tables.Where(t => t.Measures.Count() > 0).SelectMany(t => t.Measures));
    }

        
    foreach ( var m in myMeasures)
    {
        // if we are not overwriting existing descriptions then skip to the 
        // next measure if this one is not an empty string
        if (dontOverwrite && !string.IsNullOrEmpty(m.Description)) {continue; }
        
        if(debug) {
            Info("Processing: " + m.DaxObjectFullName) ;
            Info("DAX: " + m.Expression) ;
        }

        var body = 
        "{\"model\": \"gpt-4\"," +
        "\"messages\": [" +
        "{" +
        "\"role\": \"system\"," +
        "\"content\": \"You are Power BI expert. Your task is to explain the DAX calculation in a few sentences with technical aspects." + 
        "Please don't try to decipher abbreviations. \"" +
        "}," +
        "{" +
        "\"role\": \"user\"," +
        "\"content\": " + JsonConvert.SerializeObject(m.Expression.Replace("\r\n", "")) +
        "}]}";

        var res = client.PostAsync(uri, new StringContent(body, Encoding.UTF8,"application/json"));
        res.Result.EnsureSuccessStatusCode();
        var result = res.Result.Content.ReadAsStringAsync().Result;
        var obj = JObject.Parse(result);

        var desc = obj["choices"][0]["message"]["content"].ToString().Trim();
        m.Description = desc + "\n=====\n" + m.Expression;

        if(debug) {
            Info(desc);
        }
        
        callCount++; // increment the call count

        if ( callCount % apiLimit == 0) System.Threading.Thread.Sleep( sleepTime );

        if(debug) {
            if ( callCount > 3) break; // For debugging purposes
        }
    
    }
}
