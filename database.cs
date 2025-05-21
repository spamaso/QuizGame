using System.Xml.Linq;

namespace server
{
    public class DataBaseQuiz
    {
        private static DataBaseQuiz instance = null;
        private static readonly object padlock = new object();

        private SortedDictionary<string, List<DomandaQuiz>> quizList;

        private DataBaseQuiz()
        {
            quizList = new SortedDictionary<string, List<DomandaQuiz>>();
        }

        public static DataBaseQuiz GetInstance()
        {
            lock (padlock)
            {
                if (instance == null)
                {
                    instance = new DataBaseQuiz();
                }
                return instance;
            }
        }
        public void LoadData()
        {
            string pathProgetto = "C:\\Users\\tomma\\OneDrive\\Desktop\\progetto Enrico\\ServerQuiz\\database_domande.xml";

            try
            {
                XDocument file = XDocument.Load(pathProgetto);
                var domande = file.Descendants("Domanda");

                foreach (var element in domande)
                {
                    string testo = element.Element("Testo")?.Value;
                    string difficolta = element.Element("Difficolta")?.Value;
                    string categoria = element.Element("Categoria")?.Value;
                    string corretta = element.Element("Corretta")?.Value;

                    var nuovaDomanda = new DomandaQuiz(testo, difficolta, corretta);

                    foreach (var risposta in element.Elements("Risposte"))
                    {
                        nuovaDomanda.InserisciRisposta(risposta.Value);
                    }

                    if (!quizList.ContainsKey(categoria))
                        quizList[categoria] = new List<DomandaQuiz>();

                    quizList[categoria].Add(nuovaDomanda);
                }

                Console.WriteLine("Domande caricate correttamente.");
            }
            catch (Exception e)
            {
                Console.WriteLine("Errore durante il caricamento: " + e.Message);
            }
        }
        public string GetRandomQuestion(int indexCatagoria, int indexDifficolta)
        {
            string categoriaDesiderata;
            string difficoltaDesiderata;

            switch(indexCatagoria)
            {
                case 1:
                    categoriaDesiderata = "Matematica";
                    break;
                case 2:
                    categoriaDesiderata = "Geografia";
                    break;
                case 3:
                    categoriaDesiderata = "Scienze";
                    break;
                case 4:
                    categoriaDesiderata = "Cinema";
                    break;
                default:
                    Console.WriteLine("Non presente questa categoria");
                    return "null";
            }

            switch(indexDifficolta)
            {
                case 1:
                    difficoltaDesiderata = "Facile";
                    break;
                case 2:
                    difficoltaDesiderata = "Medio";
                    break;
                case 3:
                    difficoltaDesiderata = "Difficile";
                    break;
                default:
                    Console.WriteLine("Non presente questa difficoltà");
                    return "null";
            }
            
            if (quizList.ContainsKey(categoriaDesiderata) && quizList[categoriaDesiderata].Count > 0)
            {
                List<DomandaQuiz> domandeFiltrate = quizList[categoriaDesiderata]
                    .Where(domanda => domanda.Difficolta == difficoltaDesiderata)
                    .ToList();

                if (domandeFiltrate.Count > 0)
                {
                    Random random = new Random();
                    int randomIndex = random.Next(domandeFiltrate.Count);
                    DomandaQuiz domandaCasuale = domandeFiltrate[randomIndex];

                    return domandaCasuale.Testo + "|" + domandaCasuale.AlternativeRisposte[0] + "|" + domandaCasuale.AlternativeRisposte[1] + "|" + domandaCasuale.AlternativeRisposte[2];
                }
            }
            return "null";
        }
        public bool CheckSolution(int indexCategoria, string testoDomanda, int tentativoRisposta)
        {
            string categoriaDesiderata;

            switch(indexCategoria)
            {
                case 1:
                    categoriaDesiderata = "Matematica";
                    break;
                case 2:
                    categoriaDesiderata = "Geografia";
                    break;
                case 3:
                    categoriaDesiderata = "Scienze";
                    break;
                case 4:
                    categoriaDesiderata = "Cinema";
                    break;
                default:
                    Console.WriteLine("Non presente questa categoria");
                    return false;
            }

            if (quizList.ContainsKey(categoriaDesiderata) && quizList[categoriaDesiderata].Count > 0)
            {
                DomandaQuiz domandaQuiz = quizList[categoriaDesiderata]
                    .Where(domanda => testoDomanda.Contains(domanda.Testo)).ToList().First();

                if(domandaQuiz != null)
                {
                    return domandaQuiz.RispostaCorretta == tentativoRisposta.ToString();
                }
            }
            return false;
        }
    }

    public class DomandaQuiz
    {
        public string Testo { get; private set; }
        public string Difficolta { get; private set; }
        public string[] AlternativeRisposte { get; private set; }
        public string RispostaCorretta { get; private set; }

        public DomandaQuiz(string testo, string difficolta, string corretta)
        {
            Testo = testo;
            Difficolta = difficolta;
            RispostaCorretta = corretta;
            AlternativeRisposte = new string[3];
        }

        public void InserisciRisposta(string risposta)
        {
            for (int i = 0; i < AlternativeRisposte.Length; i++)
            {
                if (AlternativeRisposte[i] == null)
                {
                    AlternativeRisposte[i] = risposta;
                    break;
                }
            }
        }
    }
}
