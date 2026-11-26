namespace FantaLandia.Horror.World
{
    /// <summary>
    /// "Lucia e il buio": il cartone che passa in TV (circa 5 minuti, poi ricomincia).
    /// Una lucciola che ha paura del buio deve usare la sua piccola luce per riportare a casa una ranocchia persa nel bosco.
    /// Morale: anche la luce più piccola può aiutare chi si è perso nel buio.
    ///
    /// Formato delle righe:
    ///   "@n"          passa alla scena n (vedi CartoonRenderer)
    ///   "~s"          pausa di s secondi (solo musica)
    ///   "!luce"       la codina di Lucia si accende (da qui fino alla fine)
    ///   "chi|testo"   battuta con la voce del personaggio (lucia, tasso, gufo, rana); le voci sono in Resources/Voci
    /// </summary>
    public static class CartoonEpisode
    {
        public static readonly string[] Script =
        {
            "@0", "~2",
            "gufo|Lucia e il buio.",
            "~3",

            "@1",
            "gufo|C'era una volta, in un prato vicino al bosco, una piccola lucciola di nome Lucia.",
            "lucia|Che bel tramonto! Peccato che dopo arrivi la notte...",
            "tasso|Ciao Lucia! Perché quella faccia triste?",
            "lucia|Ciao Tasso. Io ho paura del buio. Quando il sole va giù, mi nascondo sotto una foglia.",
            "tasso|Ma tu sei una lucciola! Tu fai luce!",
            "tasso|Guarda le altre lucciole: ogni sera accendono il prato come un albero di Natale!",
            "lucia|Loro sono grandi e coraggiose. Io sono piccola e fifona.",
            "lucia|Una lucina così piccola? Non serve a niente.",
            "~2",

            "@2",
            "gufo|Quella sera, ai margini del bosco, qualcuno piangeva.",
            "rana|Aiuto! Mi sono persa! Non trovo più lo stagno!",
            "rana|Stavo inseguendo una farfalla... e adesso è tutto uguale. Alberi, alberi e ancora alberi!",
            "tasso|È la piccola Rana! Si è persa nel bosco!",
            "lucia|Nel bosco? Ma tra poco sarà buio pesto...",
            "tasso|Io ho il naso buono, ma al buio non vedo niente. Vieni con me?",
            "lucia|Io... io ho paura.",
            "tasso|Anch'io ho un po' paura. Ma se andiamo insieme, la paura diventa più piccola.",
            "~2",

            "@3",
            "gufo|Il sole se ne andò, e il bosco diventò nero come l'inchiostro.",
            "lucia|Tasso? Dove sei? Non ti vedo!",
            "tasso|Ahia! Ho sbattuto il naso contro un albero!",
            "tasso|Sono qui! Lucia, accendi la tua luce!",
            "lucia|Va bene... ci provo.",
            "gufo|Lucia tremava come una foglia. Ma pensò alla piccola Rana, sola nel bosco.",
            "~1",
            "!luce",
            "gufo|E la codina di Lucia si accese. Piccola piccola, ma calda.",
            "tasso|Ti vedo! E vedo anche il sentiero!",
            "lucia|Davvero? La mia lucina basta?",
            "~2",

            "@4",
            "gufo|Uhu! Chi va là, a quest'ora?",
            "tasso|Signor Gufo! Cerchiamo la piccola Rana, si è persa.",
            "gufo|L'ho sentita vicino ai giunchi, oltre il grande sasso. Seguite il ruscello.",
            "tasso|Grande sasso, ruscello, giunchi. Ricordatevelo, eh!",
            "lucia|Io me lo ricordo! Andiamo!",
            "lucia|Grazie, signor Gufo!",
            "gufo|E tu, piccola luce, non spegnerti. Al buio, anche una scintilla è una stella.",
            "~2",

            "@5",
            "rana|C'è qualcuno? Ho tanta paura...",
            "lucia|Rana! Siamo noi! Guarda la mia luce!",
            "rana|Sono qui! Sopra la ninfea!",
            "rana|Una stella che vola! Allora non sono sola!",
            "tasso|Vieni, piccola. Ti riportiamo a casa.",
            "rana|Ma il bosco è così buio...",
            "lucia|Stammi vicina. Io faccio luce, tu segui me.",
            "~2",

            "@6",
            "gufo|E così, in fila, attraversarono il bosco. Lucia davanti, Rana in mezzo e Tasso dietro.",
            "rana|Lucia, tu non hai paura del buio?",
            "lucia|Un po' sì. Ma adesso ho una cosa più importante da fare.",
            "tasso|Questo si chiama coraggio.",
            "rana|Il grande sasso! Ecco il ruscello!",
            "tasso|E sento già l'odore dello stagno. Ci siamo quasi!",
            "~3",

            "@7",
            "rana|Mamma! Papà! Sono a casa!",
            "rana|Lei è Lucia, e mi ha fatto luce per tutto il bosco!",
            "gufo|Quella notte, tutte le lucciole del prato vennero a vedere la piccola luce che aveva salvato la Rana.",
            "lucia|Sapete una cosa? Il buio non mi fa più così paura.",
            "tasso|E perché?",
            "lucia|Perché ho capito che anche la luce più piccola può aiutare qualcuno a tornare a casa.",
            "~3",

            "@8",
            "gufo|Anche la luce più piccola può aiutare chi si è perso nel buio. Fine.",
            "~6",
        };
    }
}
