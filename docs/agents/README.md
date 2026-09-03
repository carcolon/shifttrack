# ShiftTrack Agents

Este directorio contiene un set base de agentes internos adaptados al contexto real de ShiftTrack.

## Agentes incluidos

- `shifttrack-frontend-developer.md`
- `shifttrack-backend-architect.md`
- `shifttrack-software-architect.md`
- `shifttrack-security-engineer.md`
- `shifttrack-code-reviewer.md`
- `shifttrack-technical-writer.md`
- `shifttrack-product-manager.md`
- `shifttrack-project-manager.md`

## Contexto base de ShiftTrack

- Frontend: React, TypeScript, Vite, React Router, GSAP, Motion, SignalR.
- Backend: .NET 8, ASP.NET Core Minimal API, Dapper, SQL Server, SignalR.
- Funcionalidad principal:
  - Shift Calendar
  - Employees
  - Requests
  - PTO review
  - Swap review
- Reglas de negocio importantes:
  - Roles: Employee, Manager, Admin, Team Leader
  - PTO con restricciones por rol
  - Vacaciones contadas por días hábiles de Colombia
  - Change Request entre personas del mismo rol
  - Festivos de Colombia desde backend
  - SSO con Microsoft Entra ID

## Cómo usarlos

1. Escoge un agente según el tipo de tarea.
2. Pega el contenido del archivo como prompt de sistema, custom instruction o role prompt en tu herramienta.
3. Añade el requerimiento específico debajo.
4. Si la tarea toca varias capas, usa primero:
   - `shifttrack-product-manager`
   - luego `shifttrack-software-architect`
   - y después el agente de ejecución que corresponda

## Flujo recomendado

1. `shifttrack-product-manager`
2. `shifttrack-software-architect`
3. `shifttrack-frontend-developer` y/o `shifttrack-backend-architect`
4. `shifttrack-security-engineer`
5. `shifttrack-code-reviewer`
6. `shifttrack-technical-writer`
7. `shifttrack-project-manager`

## Nota

Estos agentes no reemplazan criterio técnico ni decisión humana. Sirven para dar consistencia, velocidad y mejor estructura al trabajo del equipo.
