CREATE TABLE IF NOT EXISTS utc_zona (
    id          SERIAL PRIMARY KEY,
    nome        TEXT NOT NULL,        -- ex: 'Central Western Standard Time'
    utc_offset  TEXT NOT NULL UNIQUE, -- ex: 'UTC+08:45'
    pais        TEXT NOT NULL,
    cidade      TEXT NOT NULL,
    descricao   TEXT,
    link_imagem TEXT,
    link_video  TEXT
);

CREATE TABLE IF NOT EXISTS previsao_tempo (
    id          SERIAL PRIMARY KEY,
    utc_id      INT REFERENCES utc_zona(id),
    data_hora   TIMESTAMP DEFAULT NOW(),
    periodo     TEXT,                 -- 'manhã', 'tarde', 'noite'
    temperatura NUMERIC(5,2),
    condicao    TEXT,
    umidade     INT,
    descricao   TEXT
);

CREATE TABLE IF NOT EXISTS log_alteracoes (
    id          SERIAL PRIMARY KEY,
    tabela      TEXT,
    operacao    TEXT,                 -- 'INSERT', 'UPDATE', 'DELETE'
    registro_id INT,
    usuario     TEXT DEFAULT CURRENT_USER,
    momento     TIMESTAMP DEFAULT NOW(),
    detalhe     TEXT
);

INSERT INTO utc_zona (nome, utc_offset, pais, cidade, descricao, link_imagem, link_video) VALUES
('Central Western Standard Time', 'UTC+08:45', 'Austrália',           'Eucla',       'Zona horária incomum usada em parte da Austrália Ocidental, próxima à fronteira com o Sul.', NULL, NULL),
('Chatham Island Standard Time',  'UTC+12:45', 'Nova Zelândia',       'Ilha Chatham', 'Fuso das Ilhas Chatham, território neozelandês isolado no Oceano Pacífico.', NULL, NULL),
('Marquesas Islands Time',        'UTC-09:30', 'França (Polinésia)',   'Nuku Hiva',   'Fuso das Ilhas Marquesas, arquipélago francês com offset negativo de meia hora incomum.', NULL, NULL),
('Nepal Standard Time',           'UTC+05:45', 'Nepal',               'Katmandu',    'Um dos poucos fusos horários do mundo com offset de 45 minutos.', NULL, NULL),
('Line Islands Time',             'UTC+14:00', 'Kiribati',            'Kiritimati',  'O fuso mais avançado do mundo — Kiritimati é o primeiro lugar a ver o nascer do novo dia.', NULL, NULL);
