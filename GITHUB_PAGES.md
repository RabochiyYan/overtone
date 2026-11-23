# Публикация игры на GitHub Pages

GitHub Pages публикует готовую WebGL-сборку из `docs/`. Папка `Builds/` исключена из Git: одного Unity-проекта для запуска сайта недостаточно.

1. Завершите Play Mode в Unity и выберите **Overtone → Build WebGL**. После успешной сборки файлы автоматически копируются в `docs/`.
2. Отправьте изменения `docs/` в GitHub вместе с кодом. Не исключайте `docs/Build/` из Git — там находится сама игра.
3. В репозитории откройте **Settings → Pages → Build and deployment**.
4. Выберите **Deploy from a branch**, ветку `main` (либо вашу основную ветку) и папку **/docs**, нажмите **Save**.
5. Дождитесь успешного развёртывания в Actions и откройте https://rabochiyyan.github.io/overtone/ .

В `docs/` должны находиться `index.html`, `.nojekyll`, `source-version.json` и папка `Build/`. ZIP-архив не заменяет эти файлы: Pages не распаковывает архивы.

При обновлении игры повторите шаги 1–2. GitHub Pages опубликует новую версию автоматически. Дату последней сборки можно проверить в `docs/source-version.json`.
