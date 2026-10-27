# Прямая загрузка Shorts на YouTube — без лимита Publora

## Состояние
- Код загрузчика: cloud-shorts/youtube_upload.py
- Оффлайн-проверки: cloud-shorts/test_youtube_upload.py
- Однократная авторизация владельца канала: cloud-shorts/oauth_bootstrap.py
- GitHub Actions: .github/workflows/youtube-direct-upload.yml
- Сейчас это безопасная **тестовая ветка** shorts-pilot-20261027.
- Продакшен-публикация через Google OAuth **пока не подключена**. Видеоролики на YouTube не загружались этим новым загрузчиком.
- Ранее опубликованный Shorts и описание не изменяются.

## Лимиты и важная оговорка

Лимит 15 публикаций в месяц — это план Publora Starter, а не общий лимит YouTube.
По официальной документации YouTube Data API (октябрь 2026) базовая квота:
100 вызовов videos.insert / сутки на Google Cloud проект; канал также имеет свой дневной
лимит, зависящий от истории и проверки. Нет гарантии 100 публичных видео в сутки.

Для Google Cloud API-проекта, созданного после 28.07.2020 и не прошедшего
YouTube API Services Compliance Audit, все загруженные через API ролики будут
ограничены приватным режимом. Это отдельная процедура от Google OAuth app verification.

OAuth приложение в режиме Testing для внешних пользователей обычно
получает refresh token с истечением через 7 дней. Для долговременной
автоматизации потребуется корректно опубликовать OAuth app в In production
и получить необходимые одобрения Google.

Документация:
- https://developers.google.com/youtube/data/determine_quota_cost
- https://developers.google.com/youtube/data/reference/rest/v3/videos/insert
- https://developers.google.com/youtube/v3/guides/quota_and_compliance_audits
- https://developers.google.com/identity/protocols/oauth2/

## Один раз настроить авторизацию (без передачи видео с компьютера)

1. Открыть https://console.cloud.google.com/ и создать отдельный Google Cloud
   проект только для загрузки YouTube. Не привязывать платные услуги.
2. Включить YouTube Data API v3 и настроить OAuth consent screen для владельца канала.
3. Создать OAuth Client ID типа **Desktop application** и скачать client_secret JSON
   на свой компьютер. Не загружать JSON в GitHub/ChatGPT.
4. Один раз выполнить на доверенном компьютере (только авторизация):
   
   python -m pip install google-auth-oauthlib
   python cloud-shorts/oauth_bootstrap.py --google-client-json путь/к/client_secret.json

   Google предложит выбрать аккаунт и подтвердить разрешения
   youtube.upload и youtube.readonly. Откроется окно браузера.
   Скрипт сохранит refresh token в домашней папке пользователя,
   **не в GitHub**. Не отправлять токен в чат и не публиковать.
5. В отдельном приватном репозитории GitHub: Settings -> Secrets and variables ->
   Actions -> Repository secrets. Добавить:
   - YOUTUBE_CLIENT_ID
   - YOUTUBE_CLIENT_SECRET
   - YOUTUBE_REFRESH_TOKEN
   В Repository variables добавить:
   - YOUTUBE_EXPECTED_CHANNEL_ID (ID канала, на который разрешено публиковать)
   - YOUTUBE_API_AUDITED = false (менять на true только после официального аудита).
6. Разместить workflow и код в default branch специально созданного проекта
   (либо после явного согласования отдельного развертывания).
   В тестовой ветке workflow_dispatch может быть недоступен на UI GitHub:
   GitHub требует наличия файла workflow в default branch.
7. Запустить GitHub Actions -> YouTube Shorts - Direct API Upload
   с ID ранее проверенной облачной сборки и privacy=private.
   Пример уже сделанной сборки: 40545329749 (артефакты живут ограниченное время).
8. Проверить видео в YouTube Studio. Только после официального аудита YouTube
   разрешать публичную автопубликацию с YOUTUBE_API_AUDITED=true.

## Как работает защита

- Отправка ведется только на канал, ID которого совпадает с YOUTUBE_EXPECTED_CHANNEL_ID.
- Перед реальной отправкой код проверяет MP4, размер, название и описание.
- Из публичного описания автоматически удаляются URL и блоки с источниками
  (твоё требование). Внутренние source_pages хранятся для учёта прав.
- В GitHub Actions по умолчанию нет ни секретов, ни автоматических публикаций:
  пуш запускает только безопасные тесты.
- При успехе upload_receipt.json содержит ID видео без OAuth ключей.
- Повтор в том же сохранённом рабочем каталоге пропускается по SHA-256.
  В отдельном новом GitHub Actions запуске receipt не подхватывается
  автоматически — не запускать одно и то же видео повторно без проверки,
  иначе может появиться дубль.
- Продакшен-очередь и расписание будут включаться только после успешной
  авторизации, теста закрытой загрузки и настройки постоянного хранилища
  idempotency-реестра. По умолчанию ничего не публикуется.

## Частота публикаций после активации

Для начала: 1–2 качественных Shorts в день (30–60 в месяц).
Важно не выпускать однотипные ролики: YouTube оценивает оригинальность и
реальную ценность контента при монетизации. Просмотры и доход не гарантированы.

## Что остаётся

Требуется однократное подтверждение Google OAuth владельцем канала и,
для полностью публичной загрузки через новый API-проект, проверка соответствия
YouTube API. Загрузка по расписанию пока не включена.
