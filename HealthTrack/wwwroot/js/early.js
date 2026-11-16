// Loaded in <head> before the page draws: marks that scripts run, so the phone layout can fold the sidebar menu
// behind its button (without scripts the menu simply stays open).
document.documentElement.classList.add('js');
