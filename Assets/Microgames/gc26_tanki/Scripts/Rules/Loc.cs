using System.Collections.Generic;

namespace GC26.Tanki.Rules
{
    /// <summary>
    /// The game's languages. Russian text is the key: T("НАЗАД") gives the text in the chosen language,
    /// and text with no entry (numbers, text already translated) comes back as it is.
    /// Strings built from parts translate each part, so the parts keep their spaces.
    /// </summary>
    public static class Loc
    {
        public static readonly string[] Names = { "РУССКИЙ", "ENGLISH", "ESPAÑOL", "PORTUGUÊS", "DEUTSCH" };
        public static int Count => Names.Length;

        /// <summary>0 Russian, 1 English, 2 Spanish, 3 Portuguese, 4 German.</summary>
        public static int Lang;

        public static string T(string ru)
        {
            if (Lang <= 0 || ru == null) return ru;
            return Table.TryGetValue(ru, out string[] row) ? row[Lang - 1] : ru;
        }

        /// <summary>The Russian key of a text shown in any language.</summary>
        public static string Ru(string shown)
        {
            if (Lang <= 0 || shown == null) return shown;
            if (Back == null)
            {
                Back = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string[]> pair in Table)
                    foreach (string text in pair.Value) Back[text] = pair.Key;
            }
            return Back.TryGetValue(shown, out string ru) ? ru : shown;
        }

        static Dictionary<string, string> Back;

        static readonly Dictionary<string, string[]> Table = Build(new[]
        {
            // ru, en, es, pt, de
            // Menus and buttons
            new[] { "СТАЛЬНОЙ РУБЕЖ", "STEEL LINE", "LÍNEA DE ACERO", "LINHA DE AÇO", "STAHLFRONT" },
            new[] { "СТАЛЬНОЙ", "STEEL", "LÍNEA", "LINHA", "STAHL" },
            new[] { "РУБЕЖ", "LINE", "DE ACERO", "DE AÇO", "FRONT" },
            new[] { "З А Щ И Т И   Р Е А К Т О Р", "D E F E N D   T H E   R E A C T O R", "D E F I E N D E   E L   R E A C T O R", "D E F E N D A   O   R E A T O R", "S C H Ü T Z E   D E N   R E A K T O R" },
            new[] { "Зарядка реактора", "Charging the reactor", "Cargando el reactor", "Carregando o reator", "Reaktor lädt" },
            new[] { "КАМПАНИЯ", "CAMPAIGN", "CAMPAÑA", "CAMPANHA", "KAMPAGNE" },
            new[] { "ШТУРМ", "ASSAULT", "ASALTO", "ASSALTO", "ANGRIFF" },
            new[] { "ВЫЖИВАНИЕ", "SURVIVAL", "SUPERVIVENCIA", "SOBREVIVÊNCIA", "ÜBERLEBEN" },
            new[] { "ГАРАЖ", "GARAGE", "GARAJE", "GARAGEM", "GARAGE" },
            new[] { "МАСТЕРСКАЯ", "WORKSHOP", "TALLER", "OFICINA", "WERKSTATT" },
            new[] { "НАГРАДЫ", "AWARDS", "LOGROS", "PRÊMIOS", "ERFOLGE" },
            new[] { "ОБУЧЕНИЕ", "TUTORIAL", "TUTORIAL", "TUTORIAL", "TUTORIAL" },
            new[] { "НАСТРОЙКИ", "SETTINGS", "AJUSTES", "OPÇÕES", "OPTIONEN" },
            new[] { "НАЗАД", "BACK", "ATRÁS", "VOLTAR", "ZURÜCK" },
            new[] { "ДАЛЬШЕ", "NEXT", "SIGUIENTE", "PRÓXIMO", "WEITER" },
            new[] { "В МЕНЮ", "MENU", "MENÚ", "MENU", "MENÜ" },
            new[] { "ЕЩЁ РАЗ", "RETRY", "REINTENTAR", "DE NOVO", "NOCHMAL" },
            new[] { "ПРОДОЛЖИТЬ", "RESUME", "CONTINUAR", "CONTINUAR", "FORTSETZEN" },
            new[] { "НАЧАТЬ ЗАНОВО", "RESTART", "REINICIAR", "REINICIAR", "NEU STARTEN" },
            new[] { "ЗВУК: ", "SOUND: ", "SONIDO: ", "SOM: ", "TON: " },
            new[] { "ВИБРАЦИЯ: ", "VIBRATION: ", "VIBRACIÓN: ", "VIBRAÇÃO: ", "VIBRATION: " },
            new[] { "ЯЗЫК: ", "LANGUAGE: ", "IDIOMA: ", "IDIOMA: ", "SPRACHE: " },
            new[] { "ВКЛ", "ON", "SÍ", "SIM", "AN" },
            new[] { "ВЫКЛ", "OFF", "NO", "NÃO", "AUS" },
            new[] { "Штурм", "Assault", "Asalto", "Assalto", "Angriff" },
            new[] { "Гараж", "Garage", "Garaje", "Garagem", "Garage" },
            new[] { "Награды", "Awards", "Logros", "Prêmios", "Erfolge" },
            new[] { "Кампания", "Campaign", "Campaña", "Campanha", "Kampagne" },
            new[] { "Мастерская", "Workshop", "Taller", "Oficina", "Werkstatt" },
            new[] { "Настройки", "Settings", "Ajustes", "Opções", "Optionen" },
            new[] { "Пауза", "Paused", "Pausa", "Pausa", "Pause" },
            new[] { "закрыта", "locked", "cerrada", "trancada", "gesperrt" },
            new[] { "закрыт", "locked", "cerrado", "trancado", "gesperrt" },
            new[] { "деталей", "parts", "piezas", "peças", "Teile" },
            new[] { "рекорд", "best", "récord", "recorde", "Rekord" },
            new[] { "звёзд", "stars", "estrellas", "estrelas", "Sterne" },
            new[] { "наград", "awards", "logros", "prêmios", "Erfolge" },
            new[] { "получено", "done", "hecho", "feito", "erhalten" },
            new[] { " дет.", " parts", " piezas", " peças", " Teile" },
            new[] { "Выбран", "Selected", "Elegido", "Escolhido", "Gewählt" },
            new[] { "Выбрать", "Select", "Elegir", "Escolher", "Wählen" },
            new[] { "Купить\n", "Buy\n", "Comprar\n", "Comprar\n", "Kaufen\n" },
            new[] { "Улучшить\n", "Upgrade\n", "Mejorar\n", "Melhorar\n", "Upgrade\n" },
            new[] { "Максимум", "Maxed", "Máximo", "Máximo", "Maximum" },
            new[] { "Теперь нападаешь ты: пробейся к вражескому реактору и разбей его. Турели бьют по прямой.",
                "Now you attack: fight through to the enemy reactor and destroy it. Turrets fire in straight lines.",
                "Ahora atacas tú: ábrete paso hasta el reactor enemigo y destrúyelo. Las torretas disparan en línea recta.",
                "Agora você ataca: abra caminho até o reator inimigo e destrua-o. As torres atiram em linha reta.",
                "Jetzt greifst du an: Kämpf dich zum feindlichen Reaktor durch und zerstöre ihn. Geschütze schießen geradeaus." },
            new[] { "У каждого танка своя способность", "Every tank has its own ability", "Cada tanque tiene su habilidad", "Cada tanque tem sua habilidade", "Jeder Panzer hat eine eigene Fähigkeit" },
            new[] { "Звёзды: победа, без потерь, быстрее нормы", "Stars: win, no losses, beat par time", "Estrellas: gana, sin bajas, bajo el tiempo", "Estrelas: vença, sem perdas, abaixo do tempo", "Sterne: Sieg, keine Verluste, unter der Zeit" },
            new[] { "Детали дают за врагов и пройденные уровни", "Parts come from enemies and cleared levels", "Las piezas salen de enemigos y niveles", "Peças vêm de inimigos e fases vencidas", "Teile gibt es für Gegner und Level" },
            new[] { "Слева джойстик, справа способность танка. Танк стреляет сам по врагам на линии ствола",
                "Stick on the left, tank ability on the right. The tank fires by itself at enemies in line with its gun",
                "Joystick a la izquierda, habilidad a la derecha. El tanque dispara solo a los enemigos en la línea del cañón",
                "Joystick à esquerda, habilidade à direita. O tanque atira sozinho nos inimigos na linha do canhão",
                "Links der Stick, rechts die Fähigkeit. Der Panzer schießt von selbst auf Gegner vor dem Rohr" },
            new[] { "Сложность (со следующего боя)", "Difficulty (from the next battle)", "Dificultad (desde la próxima batalla)", "Dificuldade (a partir da próxima batalha)", "Schwierigkeit (ab dem nächsten Kampf)" },
            new[] { "Легко", "Easy", "Fácil", "Fácil", "Leicht" },
            new[] { "Нормально", "Normal", "Normal", "Normal", "Normal" },
            new[] { "Тяжело", "Hard", "Difícil", "Difícil", "Schwer" },

            // Results
            new[] { "Крепость взята", "Fortress taken", "Fortaleza tomada", "Fortaleza tomada", "Festung erobert" },
            new[] { "Уровень пройден", "Level cleared", "Nivel superado", "Fase vencida", "Level geschafft" },
            new[] { "+10 деталей за первое прохождение", "+10 parts for the first clear", "+10 piezas por la primera vez", "+10 peças pela primeira vez", "+10 Teile fürs erste Mal" },
            new[] { "Ты всё помнишь. Удачи на рубеже!", "You remember it all. Good luck out there!", "Lo recuerdas todo. ¡Suerte en el frente!", "Você lembra de tudo. Boa sorte na linha!", "Du weißt noch alles. Viel Glück an der Front!" },
            new[] { "Обучение пройдено", "Tutorial done", "Tutorial superado", "Tutorial concluído", "Tutorial geschafft" },
            new[] { "Все крепости взяты!", "Every fortress taken!", "¡Todas las fortalezas tomadas!", "Todas as fortalezas tomadas!", "Alle Festungen erobert!" },
            new[] { "Рубеж удержан!", "The line held!", "¡La línea resistió!", "A linha resistiu!", "Die Front hält!" },
            new[] { "Вражеские реакторы погашены", "Enemy reactors shut down", "Reactores enemigos apagados", "Reatores inimigos desligados", "Feindliche Reaktoren aus" },
            new[] { "Все 20 уровней пройдены", "All 20 levels cleared", "Los 20 niveles superados", "Todas as 20 fases vencidas", "Alle 20 Level geschafft" },
            new[] { "Танки закончились", "Out of tanks", "Sin tanques", "Sem tanques", "Keine Panzer mehr" },
            new[] { "Реактор уничтожен", "Reactor destroyed", "Reactor destruido", "Reator destruído", "Reaktor zerstört" },
            new[] { "Победа", "Win", "Victoria", "Vitória", "Sieg" },
            new[] { " · без потерь", " · no losses", " · sin bajas", " · sem perdas", " · ohne Verluste" },
            new[] { " · быстрее ", " · under ", " · menos de ", " · menos de ", " · unter " },
            new[] { " · норма ", " · par ", " · tiempo ", " · tempo ", " · Ziel " },
            new[] { " с", " s", " s", " s", " s" },
            new[] { "с", "s", "s", "s", "s" },
            new[] { "ОЧКИ", "SCORE", "PUNTOS", "PONTOS", "PUNKTE" },
            new[] { "РЕКОРД", "BEST", "RÉCORD", "RECORDE", "REKORD" },
            new[] { "ДЕТАЛЕЙ", "PARTS", "PIEZAS", "PEÇAS", "TEILE" },
            new[] { "Волна ", "Wave ", "Oleada ", "Onda ", "Welle " },
            new[] { " отбита", " cleared", " superada", " vencida", " geschafft" },
            new[] { " деталей. Выбери улучшение:", " parts. Pick an upgrade:", " piezas. Elige una mejora:", " peças. Escolha uma melhoria:", " Teile. Wähle ein Upgrade:" },

            // Battle HUD
            new[] { "Турелей ", "Turrets ", "Torretas ", "Torres ", "Geschütze " },
            new[] { "Врагов ", "Enemies ", "Enemigos ", "Inimigos ", "Gegner " },
            new[] { "   Очки ", "   Score ", "   Puntos ", "   Pontos ", "   Punkte " },
            new[] { "   Звезда ", "   Star ", "   Estrella ", "   Estrela ", "   Stern " },
            new[] { "БОСС", "BOSS", "JEFE", "CHEFE", "BOSS" },
            new[] { "ВРАЖЕСКИЙ РЕАКТОР ", "ENEMY REACTOR ", "REACTOR ENEMIGO ", "REATOR INIMIGO ", "FEINDREAKTOR " },
            new[] { "РЕАКТОР ", "REACTOR ", "REACTOR ", "REATOR ", "REAKTOR " },
            new[] { "ОБУЧЕНИЕ · ШАГ ", "TUTORIAL · STEP ", "TUTORIAL · PASO ", "TUTORIAL · PASSO ", "TUTORIAL · SCHRITT " },
            new[] { " ИЗ ", " OF ", " DE ", " DE ", " VON " },
            new[] { "Реактор повреждён: осталось ", "Reactor hit: left ", "Reactor dañado: quedan ", "Reator atingido: restam ", "Reaktor getroffen: noch " },
            new[] { " готов!", " ready!", " listo!", " pronto!", " bereit!" },
            new[] { "Вражеский реактор уничтожен!", "Enemy reactor destroyed!", "¡Reactor enemigo destruido!", "Reator inimigo destruído!", "Feindreaktor zerstört!" },
            new[] { "Босс на поле!", "Boss on the field!", "¡Jefe en el campo!", "Chefe em campo!", "Boss auf dem Feld!" },
            new[] { "Награда «", "Award \"", "Logro «", "Prêmio \"", "Erfolg „" },
            new[] { "»: +", "\": +", "»: +", "\": +", "“: +" },
            new[] { " деталей", " parts", " piezas", " peças", " Teile" },

            // Bonuses
            new[] { "Звезда: орудие сильнее на ", "Star: stronger gun for ", "Estrella: cañón más fuerte durante ", "Estrela: canhão mais forte por ", "Stern: stärkere Kanone für " },
            new[] { " секунд", " seconds", " segundos", " segundos", " Sekunden" },
            new[] { "Граната: все враги уничтожены", "Grenade: every enemy destroyed", "Granada: todos los enemigos destruidos", "Granada: todos os inimigos destruídos", "Granate: alle Gegner zerstört" },
            new[] { "Щит на 8 секунд", "Shield for 8 seconds", "Escudo durante 8 segundos", "Escudo por 8 segundos", "Schild für 8 Sekunden" },
            new[] { "База укреплена бетоном", "Base walled with concrete", "Base reforzada con hormigón", "Base reforçada com concreto", "Basis mit Beton verstärkt" },
            new[] { "Враги заморожены", "Enemies frozen", "Enemigos congelados", "Inimigos congelados", "Gegner eingefroren" },
            new[] { "Дополнительная жизнь", "Extra life", "Vida extra", "Vida extra", "Extraleben" },

            // Workshop and upgrades
            new[] { "Броня", "Armor", "Blindaje", "Blindagem", "Panzerung" },
            new[] { "Пушка", "Gun", "Cañón", "Canhão", "Kanone" },
            new[] { "Скорострельность", "Fire rate", "Cadencia", "Cadência", "Feuerrate" },
            new[] { "Скорость", "Speed", "Velocidad", "Velocidade", "Tempo" },
            new[] { "+1 к здоровью танка", "+1 tank health", "+1 de vida del tanque", "+1 de vida do tanque", "+1 Panzerleben" },
            new[] { "Снаряды быстрее, на 3-м уровне двойной урон", "Faster shells, double damage at level 3", "Proyectiles más rápidos, doble daño en el nivel 3", "Tiros mais rápidos, dano dobrado no nível 3", "Schnellere Geschosse, ab Stufe 3 doppelter Schaden" },
            new[] { "Быстрее перезарядка, на 2-м уровне второй снаряд", "Faster reload, a second shell at level 2", "Recarga más rápida, segundo proyectil en el nivel 2", "Recarga mais rápida, segundo tiro no nível 2", "Schnelleres Nachladen, ab Stufe 2 ein zweites Geschoss" },
            new[] { "Танк ездит быстрее", "The tank drives faster", "El tanque va más rápido", "O tanque anda mais rápido", "Der Panzer fährt schneller" },
            new[] { "Броня +1", "Armor +1", "Blindaje +1", "Blindagem +1", "Panzerung +1" },
            new[] { "Пушка +1", "Gun +1", "Cañón +1", "Canhão +1", "Kanone +1" },
            new[] { "Скорострельность +1", "Fire rate +1", "Cadencia +1", "Cadência +1", "Feuerrate +1" },
            new[] { "Скорость +1", "Speed +1", "Velocidad +1", "Velocidade +1", "Tempo +1" },
            new[] { "Ремонт и жизнь", "Repair and life", "Reparación y vida", "Reparo e vida", "Reparatur und Leben" },
            new[] { "Быстрый заряд удара", "Faster ability charge", "Carga rápida", "Carga rápida", "Schnelle Aufladung" },
            new[] { "Бетон у реактора", "Concrete at the reactor", "Hormigón en el reactor", "Concreto no reator", "Beton am Reaktor" },
            new[] { "Больше здоровья и +1 сразу", "More health, +1 right now", "Más vida y +1 ahora", "Mais vida e +1 agora", "Mehr Leben, sofort +1" },
            new[] { "Быстрее снаряды, потом больше урона", "Faster shells, then more damage", "Proyectiles más rápidos, luego más daño", "Tiros mais rápidos, depois mais dano", "Schnellere Geschosse, dann mehr Schaden" },
            new[] { "Перезарядка короче, больше снарядов", "Shorter reload, more shells", "Recarga más corta, más proyectiles", "Recarga mais curta, mais tiros", "Kürzer nachladen, mehr Geschosse" },
            new[] { "Танк и реактор как новые, +1 жизнь", "Tank and reactor like new, +1 life", "Tanque y reactor como nuevos, +1 vida", "Tanque e reator como novos, +1 vida", "Panzer und Reaktor wie neu, +1 Leben" },
            new[] { "Удару нужно на 1 убийство меньше", "Ability needs 1 kill less", "La habilidad pide 1 baja menos", "A habilidade pede 1 abate a menos", "Fähigkeit braucht 1 Abschuss weniger" },
            new[] { "Реактор под бетоном 30 секунд", "Reactor under concrete for 30 seconds", "Reactor bajo hormigón 30 segundos", "Reator sob concreto por 30 segundos", "Reaktor 30 Sekunden unter Beton" },

            // Tutorial
            new[] { "Двигайся: джойстик слева или стрелки / WASD. Проедь пару клеток.",
                "Move: the stick on the left, or arrows / WASD. Drive a couple of cells.",
                "Muévete: joystick a la izquierda o flechas / WASD. Avanza un par de casillas.",
                "Mova-se: joystick à esquerda ou setas / WASD. Ande algumas casas.",
                "Fahr los: Stick links oder Pfeile / WASD. Fahr ein paar Felder." },
            new[] { "Танк стреляет сам. Упрись в ящик перед собой, и танк его разобьёт: ему нужно два попадания.",
                "The tank fires by itself. Drive into the crate ahead and the tank breaks it: it takes two hits.",
                "El tanque dispara solo. Empuja la caja de delante y el tanque la romperá: necesita dos impactos.",
                "O tanque atira sozinho. Encoste na caixa à frente e o tanque a quebra: são dois tiros.",
                "Der Panzer schießt von selbst. Fahr gegen die Kiste vor dir, dann zerschießt er sie: zwei Treffer." },
            new[] { "Враг! Подбей его. Здесь враги не стреляют, в бою стреляют.",
                "An enemy! Take it out. Here enemies don't shoot; in battle they do.",
                "¡Un enemigo! Destrúyelo. Aquí no disparan; en batalla sí.",
                "Um inimigo! Destrua-o. Aqui eles não atiram; na batalha atiram.",
                "Ein Gegner! Schalte ihn aus. Hier schießt er nicht, im Kampf schon." },
            new[] { "Из подсвеченных танков выпадают бонусы. Подъедь к бонусу, чтобы взять его.",
                "Glowing tanks drop bonuses. Drive over a bonus to take it.",
                "Los tanques brillantes sueltan bonus. Pasa por encima para cogerlo.",
                "Tanques brilhantes deixam bônus. Passe por cima para pegar.",
                "Leuchtende Panzer lassen Boni fallen. Fahr drüber, um ihn zu nehmen." },
            new[] { "Подбитые враги заряжают способность танка. Она заряжена: нажми «",
                "Kills charge your tank's ability. It's charged: press \"",
                "Las bajas cargan la habilidad del tanque. Está cargada: pulsa «",
                "Abates carregam a habilidade do tanque. Está carregada: toque em \"",
                "Abschüsse laden die Fähigkeit. Sie ist bereit: drück „" },
            new[] { "» внизу или E.", "\" at the bottom or E.", "» abajo o E.", "\" embaixo ou E.", "“ unten oder E." },
            new[] { "Береги реактор внизу: одно попадание в него, и бой проигран. Обучение пройдено!",
                "Guard the reactor at the bottom: one hit on it and the battle is lost. Tutorial done!",
                "Protege el reactor de abajo: un impacto y pierdes. ¡Tutorial superado!",
                "Proteja o reator embaixo: um tiro nele e a batalha está perdida. Tutorial concluído!",
                "Schütze den Reaktor unten: ein Treffer, und der Kampf ist verloren. Tutorial geschafft!" },

            // Abilities and tanks
            new[] { "Залп", "Volley", "Ráfaga", "Rajada", "Salve" },
            new[] { "Купол", "Dome", "Cúpula", "Cúpula", "Kuppel" },
            new[] { "Рывок", "Dash", "Embestida", "Investida", "Sprint" },
            new[] { "Удар реактора", "Reactor strike", "Golpe del reactor", "Golpe do reator", "Reaktorschlag" },
            new[] { "ЗАЛП", "VOLLEY", "RÁFAGA", "RAJADA", "SALVE" },
            new[] { "КУПОЛ", "DOME", "CÚPULA", "CÚPULA", "KUPPEL" },
            new[] { "РЫВОК", "DASH", "EMBESTIDA", "INVESTIDA", "SPRINT" },
            new[] { "УДАР", "STRIKE", "GOLPE", "GOLPE", "SCHLAG" },
            new[] { "Шквал", "Squall", "Borrasca", "Rajada", "Böe" },
            new[] { "Быстрый, стреляет чаще, но броня тоньше.", "Fast and fires more often, but thinner armor.", "Rápido y dispara más, pero con menos blindaje.", "Rápido e atira mais, mas com blindagem fina.", "Schnell, schießt öfter, aber dünnere Panzerung." },
            new[] { "Залп: пробивные снаряды во все 4 стороны", "Volley: piercing shells in all 4 directions", "Ráfaga: proyectiles perforantes en las 4 direcciones", "Rajada: tiros perfurantes nas 4 direções", "Salve: durchschlagende Geschosse in alle 4 Richtungen" },
            new[] { "Бастион", "Bastion", "Bastión", "Bastião", "Bastion" },
            new[] { "Медленный, зато +2 к здоровью.", "Slow, but +2 health.", "Lento, pero +2 de vida.", "Lento, mas +2 de vida.", "Langsam, dafür +2 Leben." },
            new[] { "Купол: щит на 6 с, враги рядом оглушены", "Dome: 6 s shield, nearby enemies stunned", "Cúpula: escudo 6 s, aturde a los enemigos cercanos", "Cúpula: escudo por 6 s, atordoa inimigos próximos", "Kuppel: 6 s Schild, Gegner in der Nähe betäubt" },
            new[] { "Призрак", "Phantom", "Fantasma", "Fantasma", "Phantom" },
            new[] { "Ловкий, способность копится быстрее.", "Nimble, its ability charges faster.", "Ágil, su habilidad se carga antes.", "Ágil, a habilidade carrega mais rápido.", "Wendig, die Fähigkeit lädt schneller." },
            new[] { "Рывок: прыжок на 4 клетки и невидимость 3 с", "Dash: jump 4 cells and stay unseen 3 s", "Embestida: salto de 4 casillas e invisible 3 s", "Investida: salto de 4 casas e invisível por 3 s", "Sprint: 4 Felder Sprung und 3 s unsichtbar" },
            new[] { "Страж", "Guard", "Guardián", "Guardião", "Wächter" },
            new[] { "Ровный танк без слабых мест.", "A steady tank with no weak spots.", "Un tanque equilibrado sin puntos débiles.", "Um tanque equilibrado sem pontos fracos.", "Ein solider Panzer ohne Schwächen." },
            new[] { "Удар реактора: луч до края карты", "Reactor strike: a beam to the map's edge", "Golpe del reactor: un rayo hasta el borde del mapa", "Golpe do reator: um raio até a borda do mapa", "Reaktorschlag: ein Strahl bis zum Kartenrand" },

            // Modes and maps
            new[] { "Выживание, волна ", "Survival, wave ", "Supervivencia, oleada ", "Sobrevivência, onda ", "Überleben, Welle " },
            new[] { "Обучение", "Tutorial", "Tutorial", "Tutorial", "Tutorial" },
            new[] { "Штурм ", "Assault ", "Asalto ", "Assalto ", "Angriff " },
            new[] { "Первый рубеж", "First Line", "Primera línea", "Primeira linha", "Erste Linie" },
            new[] { "Канал", "Canal", "Canal", "Canal", "Kanal" },
            new[] { "Мазутный двор", "Oil Yard", "Patio de fuel", "Pátio de óleo", "Ölhof" },
            new[] { "Узкие проходы", "Narrow Passes", "Pasos estrechos", "Passagens estreitas", "Enge Gassen" },
            new[] { "Последний заслон", "Last Barrier", "Última barrera", "Última barreira", "Letzte Sperre" },
            new[] { "Перекрёсток", "Crossroads", "Cruce", "Encruzilhada", "Kreuzung" },
            new[] { "Речной брод", "River Ford", "Vado del río", "Vau do rio", "Flussfurt" },
            new[] { "Скользкий склон", "Slippery Slope", "Pendiente resbaladiza", "Ladeira escorregadia", "Rutschiger Hang" },
            new[] { "Лабиринт", "Maze", "Laberinto", "Labirinto", "Labyrinth" },
            new[] { "Логово", "Lair", "Guarida", "Covil", "Höhle" },
            new[] { "Сеть", "Grid", "Red", "Rede", "Netz" },
            new[] { "Бетонный пояс", "Concrete Belt", "Cinturón de hormigón", "Cinturão de concreto", "Betongürtel" },
            new[] { "Затопленный квартал", "Flooded Block", "Barrio inundado", "Bairro alagado", "Überflutetes Viertel" },
            new[] { "Мазутный порт", "Oil Port", "Puerto de fuel", "Porto de óleo", "Ölhafen" },
            new[] { "Стальная крепость", "Steel Fortress", "Fortaleza de acero", "Fortaleza de aço", "Stahlfestung" },
            new[] { "Ущелье", "Gorge", "Desfiladero", "Desfiladeiro", "Schlucht" },
            new[] { "Острова", "Islands", "Islas", "Ilhas", "Inseln" },
            new[] { "Минное поле", "Minefield", "Campo minado", "Campo minado", "Minenfeld" },
            new[] { "Последняя линия", "Final Line", "Línea final", "Linha final", "Letzte Linie" },
            new[] { "Сердце реактора", "Reactor Heart", "Corazón del reactor", "Coração do reator", "Reaktorherz" },
            new[] { "Арена", "Arena", "Arena", "Arena", "Arena" },
            new[] { "Передовой пост", "Forward Post", "Puesto avanzado", "Posto avançado", "Vorposten" },
            new[] { "Мостовая застава", "Bridge Outpost", "Puesto del puente", "Posto da ponte", "Brückenposten" },
            new[] { "Мазутный склад", "Oil Depot", "Depósito de fuel", "Depósito de óleo", "Öllager" },
            new[] { "Бетонная цитадель", "Concrete Citadel", "Ciudadela de hormigón", "Cidadela de concreto", "Betonzitadelle" },
            new[] { "Лесной форт", "Forest Fort", "Fuerte del bosque", "Forte da floresta", "Waldfort" },
            new[] { "Сердце врага", "Enemy Heart", "Corazón enemigo", "Coração inimigo", "Herz des Feindes" },

            // Awards
            new[] { "Первый подбитый", "First Kill", "Primera baja", "Primeiro abate", "Erster Abschuss" },
            new[] { "Подбей 1 танк", "Destroy 1 tank", "Destruye 1 tanque", "Destrua 1 tanque", "Zerstöre 1 Panzer" },
            new[] { "Охотник", "Hunter", "Cazador", "Caçador", "Jäger" },
            new[] { "Подбей 100 танков", "Destroy 100 tanks", "Destruye 100 tanques", "Destrua 100 tanques", "Zerstöre 100 Panzer" },
            new[] { "Ветеран", "Veteran", "Veterano", "Veterano", "Veteran" },
            new[] { "Подбей 500 танков", "Destroy 500 tanks", "Destruye 500 tanques", "Destrua 500 tanques", "Zerstöre 500 Panzer" },
            new[] { "Гроза боссов", "Boss Bane", "Azote de jefes", "Terror dos chefes", "Bossschreck" },
            new[] { "Победи босса", "Beat a boss", "Vence a un jefe", "Vença um chefe", "Besiege einen Boss" },
            new[] { "Укротитель", "Tamer", "Domador", "Domador", "Bändiger" },
            new[] { "Победи 4 боссов", "Beat 4 bosses", "Vence a 4 jefes", "Vença 4 chefes", "Besiege 4 Bosse" },
            new[] { "Сносим всё", "Wrecker", "Demoledor", "Demolidor", "Abrissbirne" },
            new[] { "Разбей 200 ящиков", "Break 200 crates", "Rompe 200 cajas", "Quebre 200 caixas", "Zerstöre 200 Kisten" },
            new[] { "Сапёр", "Sapper", "Zapador", "Sapador", "Pionier" },
            new[] { "Расстреляй 10 мин", "Shoot 10 mines", "Dispara a 10 minas", "Atire em 10 minas", "Schieß 10 Minen ab" },
            new[] { "Сила реактора", "Reactor Power", "Poder del reactor", "Poder do reator", "Reaktorkraft" },
            new[] { "Используй способность 25 раз", "Use an ability 25 times", "Usa una habilidad 25 veces", "Use uma habilidade 25 vezes", "Nutze 25-mal eine Fähigkeit" },
            new[] { "Без царапин", "Not a Scratch", "Sin un rasguño", "Sem um arranhão", "Ohne Kratzer" },
            new[] { "Пройди карту без потерь", "Clear a map with no losses", "Supera un mapa sin bajas", "Vença um mapa sem perdas", "Schaff eine Karte ohne Verluste" },
            new[] { "Звёздный", "Starry", "Estelar", "Estelar", "Sternenreich" },
            new[] { "Собери 10 звёзд", "Collect 10 stars", "Consigue 10 estrellas", "Junte 10 estrelas", "Sammle 10 Sterne" },
            new[] { "Безупречный", "Flawless", "Impecable", "Impecável", "Makellos" },
            new[] { "Собери все 60 звёзд кампании", "Collect all 60 campaign stars", "Consigue las 60 estrellas de la campaña", "Junte as 60 estrelas da campanha", "Sammle alle 60 Kampagnensterne" },
            new[] { "Несгибаемый", "Unbending", "Inquebrantable", "Inabalável", "Unbeugsam" },
            new[] { "Дойди до 10-й волны в выживании", "Reach survival wave 10", "Llega a la oleada 10 en supervivencia", "Chegue à onda 10 na sobrevivência", "Erreiche Welle 10 im Überleben" },
            new[] { "Штурмовик", "Stormer", "Asaltante", "Assaltante", "Stürmer" },
            new[] { "Возьми первую крепость", "Take the first fortress", "Toma la primera fortaleza", "Tome a primeira fortaleza", "Erobere die erste Festung" },
            new[] { "Покоритель", "Conqueror", "Conquistador", "Conquistador", "Eroberer" },
            new[] { "Возьми все 6 крепостей", "Take all 6 fortresses", "Toma las 6 fortalezas", "Tome as 6 fortalezas", "Erobere alle 6 Festungen" },
            new[] { "Коллекционер", "Collector", "Coleccionista", "Colecionador", "Sammler" },
            new[] { "Собери все 4 танка", "Get all 4 tanks", "Consigue los 4 tanques", "Tenha os 4 tanques", "Hol dir alle 4 Panzer" },
        });

        static Dictionary<string, string[]> Build(string[][] rows)
        {
            var table = new Dictionary<string, string[]>();
            foreach (string[] row in rows)
            {
                var texts = new string[row.Length - 1];
                System.Array.Copy(row, 1, texts, 0, texts.Length);
                table[row[0]] = texts;
            }
            return table;
        }

        /// <summary>Russian keys with a missing or empty translation, for the tests.</summary>
        public static IEnumerable<string> Gaps()
        {
            foreach (KeyValuePair<string, string[]> pair in Table)
                if (pair.Value.Length != Count - 1 || System.Array.Exists(pair.Value, string.IsNullOrEmpty)) yield return pair.Key;
        }

        public static bool Has(string ru) => Table.ContainsKey(ru);
    }
}
