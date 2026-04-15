CREATE OR REPLACE FUNCTION fn_dados_relatorio()
RETURNS TABLE (
    zona_nome      TEXT,
    zona_utc_offset TEXT,
    pais           TEXT,
    cidade         TEXT,
    zona_descricao TEXT,
    link_imagem    TEXT,
    link_video     TEXT,
    periodo        TEXT,
    temperatura    NUMERIC,
    condicao       TEXT,
    umidade        INT,
    data_hora      TIMESTAMP
)
LANGUAGE sql STABLE AS $$
    SELECT
        z.nome,
        z.utc_offset,
        z.pais,
        z.cidade,
        z.descricao,
        z.link_imagem,
        z.link_video,
        p.periodo,
        p.temperatura,
        p.condicao,
        p.umidade,
        p.data_hora
    FROM utc_zona z
    LEFT JOIN previsao_tempo p ON p.utc_id = z.id
    WHERE p.data_hora::DATE = CURRENT_DATE
    ORDER BY z.utc_offset, p.periodo;
$$;

CREATE OR REPLACE FUNCTION fn_log_hoje()
RETURNS TABLE (
    operacao TEXT,
    tabela   TEXT,
    detalhe  TEXT,
    momento  TIMESTAMP
)
LANGUAGE sql STABLE AS $$
    SELECT operacao, tabela, detalhe, momento
    FROM log_alteracoes
    WHERE momento::DATE = CURRENT_DATE
    ORDER BY momento DESC
    LIMIT 20;
$$;
