using System;
using System.Collections.Generic;
using GC26.Core;
using UnityEngine;

namespace GC26.Spinv
{
    // Вход игры для общего меню GC26 (IMicrogame) и переходы внутри одной сцены:
    // наше меню и выключенный шаблон игры лежат в сцене вместе. По PLAY создаётся рабочая копия шаблона,
    // при выходе в меню или рестарте она удаляется вместе со всем, что игра наспавнила за время работы.
    // Шаблон выключен и ничего не тратит, во время игры выключено меню.
    public class SpinvMicrogame : MonoBehaviour, IMicrogame
    {
        public const string Id = "gc26_spinv";

        [Tooltip("Наше главное меню со всем, что к нему относится")]
        public GameObject menuRoot;
        [Tooltip("Шаблон игры: всегда выключен, по PLAY с него делается копия")]
        public GameObject gameTemplate;

        // Выход в общее меню. POWER OFF по решению автора закрывает приложение, поэтому сейчас не вызывается
        Action<int> exit;
        GameObject currentGame;

        // Идёт ли сейчас обучение (кнопка TUTORIAL): игра узнаёт это при создании копии
        public bool TutorialRun { get; private set; }
        // Обучение аркады (кнопка «?» рядом с ARCADE MODE): тоже обучение, но про метки волн и циклы
        public bool ArcadeTutorialRun { get; private set; }
        readonly HashSet<GameObject> menuObjects = new HashSet<GameObject>();

        public void Begin(Action<int> exitCallback)
        {
            exit = exitCallback;
        }

        void Awake()
        {
            gameTemplate.SetActive(false);
            menuRoot.SetActive(true);
        }

        public void StartGame(bool tutorial = false, bool arcadeTutorial = false)
        {
            TutorialRun = tutorial || arcadeTutorial;
            ArcadeTutorialRun = arcadeTutorial;
            // Всё, что лежит в сцене до запуска, - меню и общие объекты; остальное потом считается игрой
            menuObjects.Clear();
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                menuObjects.Add(root);

            menuRoot.SetActive(false);
            currentGame = Instantiate(gameTemplate);
            currentGame.name = "Game";
            currentGame.SetActive(true);
        }

        public void Restart()
        {
            ClearGame();
            StartGame(TutorialRun, ArcadeTutorialRun);
        }

        public void ToMenu()
        {
            ClearGame();
            TutorialRun = false;
            ArcadeTutorialRun = false;
            menuRoot.SetActive(true);
        }

        // Меню заново, как после загрузки сцены (после полного сброса сохранений)
        public void ReloadMenu()
        {
            menuRoot.SetActive(false);
            menuRoot.SetActive(true);
        }

        void ClearGame()
        {
            RestoreGlobals();
            if (currentGame != null)
            {
                currentGame.SetActive(false); // OnDisable сразу, Destroy - в конце кадра
                Destroy(currentGame);
                currentGame = null;
            }
            // Пули, взрывы, монеты, искры и прочее, созданное игрой в корне сцены
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (menuObjects.Contains(root)) continue;
                root.SetActive(false);
                Destroy(root);
            }
        }

        void OnDisable()
        {
            RestoreGlobals();
        }

        // Глобальное, что игра меняет: время, звук, настройки CRT
        static void RestoreGlobals()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            AudioListener.volume = 1f; // моргание глушит звук
            CrtBoost.Set(0f);
            CrtBoost.SetBlur(0f);
            // Партия закончилась не выключением (выход в меню, рестарт): музыки победы нет, фон играет
            SpinvUiSound.StopMusic();
            SpinvAmbience.Resume();
        }
    }
}
