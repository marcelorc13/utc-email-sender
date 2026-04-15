CREATE TABLE IF NOT EXISTS utc_zona (
    id            SERIAL PRIMARY KEY,
    nome          TEXT NOT NULL,
    utc_offset    TEXT NOT NULL UNIQUE,
    pais          TEXT NOT NULL,
    cidade        TEXT NOT NULL,
    descricao     TEXT,
    weather_query TEXT NOT NULL         -- lat,lon para WeatherAPI q=
);

CREATE TABLE IF NOT EXISTS previsao_tempo (
    id          SERIAL PRIMARY KEY,
    utc_id      INT REFERENCES utc_zona(id),
    data_hora   TIMESTAMP DEFAULT NOW(),
    periodo     TEXT CHECK (periodo IN ('manhã', 'tarde', 'noite')),
    temperatura NUMERIC(5,2),
    condicao    TEXT,
    umidade     INT,
    descricao   TEXT
);

CREATE TABLE IF NOT EXISTS log_alteracoes (
    id          SERIAL PRIMARY KEY,
    tabela      TEXT,
    operacao    TEXT CHECK (operacao IN ('INSERT', 'UPDATE', 'DELETE')),
    registro_id INT,
    usuario     TEXT DEFAULT CURRENT_USER,
    momento     TIMESTAMP DEFAULT NOW(),
    detalhe     TEXT
);

INSERT INTO utc_zona (nome, utc_offset, pais, cidade, descricao, weather_query) VALUES
('Central Western Standard Time', 'UTC+08:45', 'Austrália',           'Eucla',        'Zona horária incomum usada em parte da Austrália Ocidental, próxima à fronteira com o Sul.','-31.7,128.9'),
('Chatham Island Standard Time',  'UTC+12:45', 'Nova Zelândia',       'Ilha Chatham', 'Fuso das Ilhas Chatham, território neozelandês isolado no Oceano Pacífico.','-43.9,-176.5'),
('Marquesas Islands Time',        'UTC-09:30', 'França (Polinésia)',   'Nuku Hiva',    'Fuso das Ilhas Marquesas, arquipélago francês com offset negativo de meia hora incomum.','-8.9,-140.1'),
('Nepal Standard Time',           'UTC+05:45', 'Nepal',               'Katmandu',     'Um dos poucos fusos horários do mundo com offset de 45 minutos.','27.7,85.3'),
('Line Islands Time',             'UTC+14:00', 'Kiribati',            'Kiritimati',   'O fuso mais avançado do mundo — Kiritimati é o primeiro lugar a ver o nascer do novo dia.', '1.87,-157.4');
