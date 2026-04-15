-- =============================================================
-- 02_triggers.sql — Triggers do Projeto 1UP
-- =============================================================

-- -------------------------------------------------------------
-- Trigger 1: Registra log automático em log_alteracoes
-- Disparado em INSERT ou UPDATE na tabela previsao_tempo
-- -------------------------------------------------------------

CREATE OR REPLACE FUNCTION fn_log_previsao()
RETURNS TRIGGER
LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO log_alteracoes (tabela, operacao, registro_id, detalhe)
    VALUES (
        'previsao_tempo',
        TG_OP,
        NEW.id,
        format('UTC ID %s | %s | %s°C | %s', NEW.utc_id, NEW.periodo, NEW.temperatura, NEW.condicao)
    );
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_log_previsao
AFTER INSERT OR UPDATE ON previsao_tempo
FOR EACH ROW EXECUTE FUNCTION fn_log_previsao();

-- -------------------------------------------------------------
-- Trigger 2: Notifica o Worker C# via NOTIFY
-- Disparado em INSERT na tabela previsao_tempo
-- O payload contém apenas o timestamp — o HTML é gerado no C#
-- -------------------------------------------------------------

CREATE OR REPLACE FUNCTION fn_notify_relatorio()
RETURNS TRIGGER
LANGUAGE plpgsql AS $$
BEGIN
    PERFORM pg_notify('novo_relatorio', NOW()::TEXT);
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_notify_relatorio
AFTER INSERT ON previsao_tempo
FOR EACH ROW EXECUTE FUNCTION fn_notify_relatorio();
