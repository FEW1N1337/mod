#!/bin/sh
/usr/local/bin/python3 /home/user/mod/beyin.py update
beyin_update_exit=$?
printf "\nKapatmak icin Enter tusuna basin. "
read -r beyin_update_reply || true
exit "$beyin_update_exit"
