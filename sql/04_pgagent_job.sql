-- Criar o job
INSERT INTO pgagent.pga_job (jobjclid, jobname, jobdesc, jobenabled)
VALUES (
    1,
    'job_relatorio_utc_manha',
    'Sinaliza o Worker da aplicação para envio do relatório UTC',
    TRUE
);

--  emite NOTIFY
INSERT INTO pgagent.pga_jobstep (jstjobid, jstname, jstenabled, jstkind, jstcode, jstonerror)
VALUES (
    currval('pgagent.pga_job_jobid_seq'),
    'step_notify_email',
    TRUE,
    's',  -- SQL step
    'SELECT pg_notify(''novo_relatorio'', NOW()::TEXT);',
    'f'   -- falha em caso de erro
);

-- schedule: diariamente às 06:00
INSERT INTO pgagent.pga_schedule (
    jscjobid, jscname, jscenabled, jscstart,
    jscminutes, jschours, jscweekdays, jscmonthdays, jscmonths
)
VALUES (
    currval('pgagent.pga_job_jobid_seq'),
    'schedule_diario_manha',
    TRUE,
    NOW(),
    -- minutos: apenas o minuto 0
    '{t,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f}',
    -- horas: apenas a hora 6
    '{f,f,f,f,f,f,t,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f}',
    -- dias da semana: todos (dom a sab)
    '{t,t,t,t,t,t,t}',
    -- dias do mês: todos
    '{f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f,f}',
    -- meses: todos
    '{t,t,t,t,t,t,t,t,t,t,t,t}'
);
