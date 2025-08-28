# 🚀 Подробное руководство по установке и настройке

## 📋 Пошаговая установка

### Шаг 1: Подготовка сервера
```bash
# Убедитесь что на сервере установлен Oxide/uMod
# Проверить можно командой в консоли сервера:
version
```

### Шаг 2: Загрузка плагина
1. Скачайте файл `SleepingBagLabels.cs`
2. Поместите его в папку: `server_folder/oxide/plugins/`

### Шаг 3: Установка плагина
```bash
# В консоли сервера выполните:
oxide.load SleepingBagLabels
```

### Шаг 4: Проверка установки
```bash
# Проверьте что плагин загружен:
oxide.plugins
# В списке должен появиться SleepingBagLabels
```

---

## ⚙️ Первичная настройка

### Настройка прав доступа

```bash
# Дать права всем игрокам:
oxide.grant group default sleepingbaglabels.use

# Дать права конкретному игроку:
oxide.grant user 76561198000000000 sleepingbaglabels.use

# Дать админские права:
oxide.grant user 76561198000000000 sleepingbaglabels.admin
```

### Базовая конфигурация

После первого запуска плагин создаст файл конфигурации:
`server_folder/oxide/config/SleepingBagLabels.json`

**Рекомендуемые настройки для начала:**

```json
{
  "Display Settings": {
    "Show sleeping bag labels": true,
    "Label height offset": 1.5,
    "Font size": 12,
    "Max display distance": 30.0,
    "Show only when looking at bag": false,
    "Show background": true
  },
  "Color Settings": {
    "Teammate color (hex)": "#00FF00",
    "Enemy color (hex)": "#FF4444", 
    "Neutral color (hex)": "#FFAA00",
    "Own sleeping bag color (hex)": "#00AAFF"
  }
}
```

---

## 🎨 Настройка для разных типов серверов

### 🏟️ PvP сервер
```json
{
  "Display Settings": {
    "Max display distance": 25.0,
    "Show only when looking at bag": true
  },
  "Color Settings": {
    "Enemy color (hex)": "#FF0000"
  }
}
```

### 🏡 PvE сервер  
```json
{
  "Display Settings": {
    "Max display distance": 75.0,
    "Show only when looking at bag": false
  },
  "Color Settings": {
    "Enemy color (hex)": "#FFA500"
  }
}
```

### 📺 Стримерский сервер
```json
{
  "Streamer Settings": {
    "Hide player names": true,
    "Replace name with text": "Игрок",
    "Show only team/enemy indicator": true,
    "Teammate indicator": "Союзник",
    "Enemy indicator": "Противник"
  }
}
```

---

## 🔧 Расширенная настройка

### Настройка производительности

**Для слабых серверов:**
```json
{
  "Performance Settings": {
    "Update frequency (seconds)": 2.0,
    "Max labels per player": 10,
    "Enable distance culling": true
  },
  "Display Settings": {
    "Max display distance": 20.0
  }
}
```

**Для мощных серверов:**
```json
{
  "Performance Settings": {
    "Update frequency (seconds)": 0.5,
    "Max labels per player": 50,
    "Enable distance culling": false
  },
  "Display Settings": {
    "Max display distance": 100.0
  }
}
```

### Кастомные цвета

**Тематические цвета для разных серверов:**

🌟 **Неоновая тема:**
```json
{
  "Color Settings": {
    "Teammate color (hex)": "#00FFAA",
    "Enemy color (hex)": "#FF0088", 
    "Neutral color (hex)": "#FFAA00",
    "Own sleeping bag color (hex)": "#AA00FF"
  }
}
```

🌙 **Темная тема:**
```json
{
  "Color Settings": {
    "Teammate color (hex)": "#88FF88",
    "Enemy color (hex)": "#CC4444", 
    "Neutral color (hex)": "#CCAA44",
    "Own sleeping bag color (hex)": "#4488CC"
  }
}
```

---

## 🎮 Примеры команд для игроков

### Основные команды
```
/sleepingbag - показать справку
/sleepingbag toggle - включить/выключить метки  
/sleepingbag distance 40 - установить расстояние 40 метров
```

### Команды для администраторов
```bash
# Перезагрузить плагин:
oxide.reload SleepingBagLabels

# Перезагрузить только конфигурацию:
/sleepingbag reload

# Посмотреть статус плагина:
oxide.show SleepingBagLabels
```

---

## 🐛 Устранение проблем

### Проблема: Метки не отображаются

**Возможные причины и решения:**

1. **Нет прав доступа**
   ```bash
   oxide.grant user <SteamID> sleepingbaglabels.use
   ```

2. **Плагин не загружен**
   ```bash
   oxide.load SleepingBagLabels
   ```

3. **Настройки отключены**
   - Проверьте в конфиге: `"Show sleeping bag labels": true`

4. **Слишком большое расстояние**
   - Уменьшите `"Max display distance"` в конфиге

### Проблема: Неправильные цвета

**Решения:**

1. **Проверьте HEX коды**
   - Убедитесь что цвета в формате "#RRGGBB"
   - Используйте только валидные HEX символы (0-9, A-F)

2. **Игроки не в команде**
   - Метки будут нейтрального цвета если игроки не в команде

3. **Перезагрузите плагин**
   ```bash
   oxide.reload SleepingBagLabels
   ```

### Проблема: Низкая производительность

**Оптимизация:**

1. **Увеличьте частоту обновления**
   ```json
   "Update frequency (seconds)": 2.0
   ```

2. **Уменьшите расстояние отображения**
   ```json
   "Max display distance": 25.0
   ```

3. **Включите кулинг**
   ```json
   "Enable distance culling": true
   ```

4. **Ограничьте количество меток**
   ```json
   "Max labels per player": 15
   ```

---

## 📊 Мониторинг производительности

### Команды для проверки

```bash
# Проверить загрузку сервера:
perf

# Проверить использование памяти плагином:
oxide.show SleepingBagLabels

# Проверить время выполнения команд:
oxide.time SleepingBagLabels
```

### Рекомендуемые значения

| Параметр | Малый сервер (≤50) | Средний сервер (50-150) | Большой сервер (≥150) |
|----------|-------------------|------------------------|----------------------|
| Update frequency | 1.0 | 1.5 | 2.0 |
| Max distance | 50.0 | 40.0 | 30.0 |
| Max labels | 25 | 20 | 15 |

---

## 🔄 Обновление плагина

### Процедура обновления

1. **Сохраните конфигурацию**
   ```bash
   cp oxide/config/SleepingBagLabels.json oxide/config/SleepingBagLabels.json.backup
   ```

2. **Выгрузите старую версию**
   ```bash
   oxide.unload SleepingBagLabels
   ```

3. **Замените файл плагина**
   - Скачайте новую версию
   - Замените файл в `oxide/plugins/`

4. **Загрузите новую версию**
   ```bash
   oxide.load SleepingBagLabels
   ```

5. **Проверьте конфигурацию**
   - Плагин автоматически добавит новые настройки
   - Старые настройки сохранятся

---

## 💡 Полезные советы

### Для администраторов

1. **Регулярно проверяйте логи** на предмет ошибок
2. **Тестируйте изменения** на тестовом сервере
3. **Делайте бэкапы конфигурации** перед изменениями
4. **Следите за производительностью** при большом количестве игроков

### Для стримеров

1. Используйте **стримерский режим** для скрытия никнеймов
2. Настройте **нейтральные цвета** чтобы не выдавать союзников
3. Установите **разумное расстояние** отображения

### Для игроков

1. **Изучите цветовую схему** вашего сервера
2. **Используйте команды** для персональной настройки
3. **Сообщайте админам** о проблемах или предложениях

---

## 📞 Техническая поддержка

### Информация для отчета о проблеме

При обращении в поддержку предоставьте:

1. **Версию плагина**
2. **Версию Oxide/uMod**
3. **Версию Rust сервера**
4. **Файл конфигурации**
5. **Логи сервера** с ошибками
6. **Описание проблемы** и шаги для воспроизведения

### Формат отчета

```
Версия плагина: 1.0.0
Версия Oxide: 2.0.5000
Версия Rust: 2023.12.7
Количество игроков: 45
Описание проблемы: Метки не отображаются для некоторых игроков
Шаги воспроизведения: 
1. Игрок подходит к спальному мешку
2. Метка не появляется
3. В логах нет ошибок
```

---

<div align="center">

**🎉 Спасибо за использование Sleeping Bag Labels!**

*Сделайте свой сервер еще лучше* 🚀

</div>