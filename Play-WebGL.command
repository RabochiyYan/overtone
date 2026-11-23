#!/bin/zsh
cd "${0:A:h}" || exit 1
if [[ ! -f Builds/WebGL/index.html ]]; then
  print 'Сначала соберите игру: Unity → Overtone → Build WebGL.'
  read '?Нажмите Enter для выхода…'
  exit 1
fi
print 'Игра: http://localhost:8080'
print 'Чтобы остановить сервер, нажмите Ctrl+C или закройте это окно.'
open 'http://localhost:8080'
python3 -m http.server 8080 --bind 127.0.0.1 --directory Builds/WebGL
