#!/bin/zsh
cd "${0:A:h}" || exit 1
python3 Tools/play_webgl.py "$@"
launch_result=$?
if (( launch_result != 0 )); then
  read '?Нажмите Enter для выхода…'
fi
exit $launch_result
