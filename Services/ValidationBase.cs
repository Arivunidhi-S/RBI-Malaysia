using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace RBI_Malaysia.Services
{
    public class ValidationBase : ComponentBase
    {
        protected EditContext? CurrentEditContext;

        protected ValidationMessageStore? ValidationMessages;

        protected void InitializeValidation(object model)
        {
            CurrentEditContext = new EditContext(model);
            ValidationMessages = new ValidationMessageStore(CurrentEditContext);

            CurrentEditContext.OnValidationRequested += ValidateModel;
            CurrentEditContext.OnFieldChanged += ValidateField;
        }

        private void ValidateModel(
            object? sender,
            ValidationRequestedEventArgs e)
        {
            if (CurrentEditContext == null ||
                ValidationMessages == null)
                return;

            ValidationMessages.Clear();

            var model = CurrentEditContext.Model;

            var validationContext =
                new ValidationContext(model);

            var results =
                new List<ValidationResult>();

            Validator.TryValidateObject(
                model,
                validationContext,
                results,
                true);

            foreach (var result in results)
            {
                var members = result.MemberNames.Any()
                    ? result.MemberNames
                    : new[] { string.Empty };

                foreach (var member in members)
                {
                    CurrentEditContext.NotifyFieldChanged(
                        new FieldIdentifier(
                            model,
                            member));

                    ValidationMessages.Add(
                        new FieldIdentifier(
                            model,
                            member),
                        result.ErrorMessage ?? "Invalid value.");
                }
            }

            CurrentEditContext.NotifyValidationStateChanged();
        }

        private void ValidateField(
            object? sender,
            FieldChangedEventArgs e)
        {
            if (CurrentEditContext == null ||
                ValidationMessages == null)
                return;

            ValidationMessages.Clear(e.FieldIdentifier);

            var validationContext =
                new ValidationContext(
                    CurrentEditContext.Model)
                {
                    MemberName = e.FieldIdentifier.FieldName
                };

            var property =
                CurrentEditContext.Model
                    .GetType()
                    .GetProperty(
                        e.FieldIdentifier.FieldName);

            if (property == null)
                return;

            var value = property.GetValue(
                CurrentEditContext.Model);

            var results =
                new List<ValidationResult>();

            Validator.TryValidateProperty(
                value,
                validationContext,
                results);

            foreach (var result in results)
            {
                ValidationMessages.Add(
                    e.FieldIdentifier,
                    result.ErrorMessage ?? "Invalid value.");
            }

            CurrentEditContext.NotifyValidationStateChanged();
        }

        protected bool ValidateForm()
        {
            if (CurrentEditContext == null)
                return false;

            return CurrentEditContext.Validate();
        }

        protected void DisposeValidation()
        {
            if (CurrentEditContext != null)
            {
                CurrentEditContext.OnValidationRequested -= ValidateModel;
                CurrentEditContext.OnFieldChanged -= ValidateField;
            }
        }
    }
}